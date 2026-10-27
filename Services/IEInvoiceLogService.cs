using CygnetEInvoice.Models;
using Dapper;
using Mgcerp.Infrastructure.Helpers;

using Mgcerp.Models.DTOModels.EInvoice;
using Mgcerp.Models.EInvoice;
using System.Globalization;
using System.Text.Json;

namespace Mgcerp.Application.Services
{
    public interface IEInvoiceLogService
    {
        // IEInvoiceService
        Task<long> InsertAsync(CygnetDocument doc, string erpDocumentNumber, string status,
                               string? referenceId, IReadOnlyList<string> errors,
                               CygnetApiResult? submit, DateTime? nextCheckAt,
                               string? requestPayload, string? submittedBy,
                               CancellationToken ct = default);
        /// <summary>Claims due rows (leases them for a few minutes so parallel workers don't double-check).</summary>
        Task<IReadOnlyList<DueSubmission>> ClaimDueAsync(int batchSize, int leaseMinutes = 5, CancellationToken ct = default);

        Task UpdateStatusAsync(long id, CygnetOutcome outcome, string? statusText, IReadOnlyList<string> errors,
                               int? httpCode, string? rawResponse, DateTime? nextCheckAt, CancellationToken ct = default);
    }
    public class EInvoiceLogService :IEInvoiceLogService
    {
        private readonly IDapperHelper _dapper;
        public EInvoiceLogService(IDapperHelper dapper)
        {
            _dapper= dapper; 

        }

        

        public async Task<long> InsertAsync(CygnetDocument doc, string erpDocumentNumber, string status,
                                            string? referenceId, IReadOnlyList<string> errors,
                                            CygnetApiResult? submit, DateTime? nextCheckAt,
                                            string? requestPayload, string? submittedBy,
                                            CancellationToken ct = default)
        {
            const string sql = @"
                        INSERT INTO dbo.einvoiceSubmission
                        ( ErpDocumentNumber, SubmittedDocumentNumber, DocumentUuid, SupplyType, DocumentType, DocumentTypePurpose,
                          TransactionTypeCode, DocumentDate, DocumentCurrency, ExchangeRate, TotalDocumentAmount, TotalDocAmountInAccCurrency,
                          EntityTaxRegNo, BillToTaxRegNo, BillToLegalName, ReferenceId,
                          Status, ErrorMessages, NextCheckAt, CompletedAt,
                          SubmitHttpCode, SubmitResponse, RequestPayload, SubmittedBy )
                        OUTPUT inserted.Id
                        VALUES
                        ( @ErpDocumentNumber, @SubmittedDocumentNumber, @DocumentUuid, @SupplyType, @DocumentType, @DocumentTypePurpose,
                          @TransactionTypeCode, @DocumentDate, @DocumentCurrency, @ExchangeRate, @TotalDocumentAmount, @TotalDocAmountInAccCurrency,
                          @EntityTaxRegNo, @BillToTaxRegNo, @BillToLegalName, @ReferenceId,
                          @Status, @ErrorMessages, @NextCheckAt,
                          CASE WHEN @Status IN ('Success','Failed') THEN SYSDATETIME() END,
                          @SubmitHttpCode, @SubmitResponse, @RequestPayload, @SubmittedBy );";

            var ids = await _dapper.QueryAsync<long>(sql, new
            {
                ErpDocumentNumber = erpDocumentNumber,
                SubmittedDocumentNumber = doc.DocumentNumber,
                DocumentUuid = string.IsNullOrWhiteSpace(doc.DocumentUuid) ? null : doc.DocumentUuid,
                doc.SupplyType,
                doc.DocumentType,
                doc.DocumentTypePurpose,
                doc.TransactionTypeCode,
                DocumentDate = DateTime.ParseExact(doc.DocumentDate[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                doc.DocumentCurrency,
                ExchangeRate = Dec(doc.ExchangeRate),
                TotalDocumentAmount = Dec(doc.TotalDocumentAmount) ?? 0m,
                TotalDocAmountInAccCurrency = Dec(doc.TotalDocAmountInAccCurrency),
                doc.EntityTaxRegNo,
                doc.BillToTaxRegNo,
                doc.BillToLegalName,
                ReferenceId = new DbString { Value = referenceId, IsAnsi = true, Length = 64 },
                Status = status,
                ErrorMessages = errors.Count == 0 ? null : JsonSerializer.Serialize(errors),
                NextCheckAt = nextCheckAt,
                SubmitHttpCode = submit?.StatusCode ?? 0,
                SubmitResponse = submit?.RawResponse,
                RequestPayload = requestPayload,
                SubmittedBy = submittedBy
            });

            return ids.Single();
        }

        public async Task<IReadOnlyList<DueSubmission>> ClaimDueAsync(int batchSize, int leaseMinutes = 5, CancellationToken ct = default)
        {
            const string sql = @"
                WITH due AS
                (
                    SELECT TOP (@BatchSize) *
                    FROM   dbo.einvoiceSubmission WITH (ROWLOCK, UPDLOCK, READPAST)
                    WHERE  NextCheckAt IS NOT NULL
                      AND  NextCheckAt <= SYSDATETIME()
                    ORDER BY NextCheckAt
                )
                UPDATE due
                SET    NextCheckAt = DATEADD(MINUTE, @LeaseMinutes, SYSDATETIME())
                OUTPUT inserted.Id, inserted.ReferenceId, inserted.StatusAttempts, inserted.SubmittedAt;";

            var rows = await _dapper.QueryAsync<DueSubmission>(sql, new { BatchSize = batchSize, LeaseMinutes = leaseMinutes });
            return rows.ToList();
        }

        public async Task UpdateStatusAsync(long id, CygnetOutcome outcome, string? statusText, IReadOnlyList<string> errors,
                                            int? httpCode, string? rawResponse, DateTime? nextCheckAt, CancellationToken ct = default)
        {
            const string sql = @"
                UPDATE dbo.einvoiceSubmission
                SET    Status             = @Status,
                       StatusText         = @StatusText,
                       ErrorMessages      = @ErrorMessages,
                       StatusAttempts     = StatusAttempts + 1,
                       LastCheckedAt      = SYSDATETIME(),
                       LastStatusHttpCode = @HttpCode,
                       LastStatusResponse = @RawResponse,
                       NextCheckAt        = @NextCheckAt,
                       CompletedAt        = CASE WHEN @Status IN ('Success','Failed') THEN SYSDATETIME() ELSE CompletedAt END,
                       UpdatedAt          = SYSDATETIME()
                WHERE  Id = @Id;

                INSERT INTO dbo.einvoiceSubmissionLog (SubmissionId, HttpCode, Status, StatusText, RawResponse)
                VALUES (@Id, @HttpCode, @Status, @StatusText, @RawResponse);

                SELECT @@ROWCOUNT;";

            await _dapper.QueryAsync<int>(sql, new
            {
                Id = id,
                Status = ToDbStatus(outcome),
                StatusText = statusText,
                ErrorMessages = errors.Count == 0 ? null : JsonSerializer.Serialize(errors),
                HttpCode = httpCode,
                RawResponse = rawResponse,
                NextCheckAt = nextCheckAt
            });
        }

        private static decimal? Dec(string? s) =>
            decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
        public static string ToDbStatus(CygnetOutcome outcome) => outcome.ToString();   // matches CK_einvSub_Status
    }

}
