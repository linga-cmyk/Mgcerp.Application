using CygnetEInvoice.Models;
using Mgcerp.Infrastructure.Helpers;
using Mgcerp.Models.DTOModels.EInvoice;
using System.Data;

namespace Mgcerp.Application.Common
{
    public class ApplicationCommon
    {
        public static DateTime GetCurrentDateTime() => DateTime.Now;
        public static async Task<decimal> GetNewDocumentNumber(
                   string Co,
                   DateTime DocumentDate,
                   string TransactionCode,
                   IDapperHelper dapper,
                   IDbTransaction? txn = null)
        {
            if (txn == null)
                throw new ArgumentNullException(nameof(txn));

            string monthCode = Co + DocumentDate.ToString("yyMM");

            const string selectSql = @"
        SELECT vr_no
        FROM dbo.trcwise_lasno WITH (UPDLOCK, HOLDLOCK)
        WHERE trc_code = @TransactionCode
          AND month_code = @MonthCode;";

            decimal? currentSerial =
                await dapper.ExecuteScalarAsync<decimal?>(
                    selectSql,
                    new
                    {
                        TransactionCode,
                        MonthCode = monthCode
                    },
                    txn);

            decimal nextSerial = (currentSerial ?? 0m) + 1m;

            if (currentSerial.HasValue)
            {
                await dapper.ExecuteTransactionAsync(
                    @"
            UPDATE dbo.trcwise_lasno
            SET vr_no = @SerialNo
            WHERE trc_code = @TransactionCode
              AND month_code = @MonthCode;",
                    new
                    {
                        TransactionCode,
                        MonthCode = monthCode,
                        SerialNo = nextSerial
                    },
                    txn);
            }
            else
            {
                await dapper.ExecuteTransactionAsync(
                    @"
            INSERT INTO dbo.trcwise_lasno
                (trc_code, month_code, vr_no)
            VALUES
                (@TransactionCode, @MonthCode, @SerialNo);",
                    new
                    {
                        TransactionCode,
                        MonthCode = monthCode,
                        SerialNo = nextSerial
                    },
                    txn);
            }

            // IMPORTANT: Use a custom numeric format, not "D4".
            string documentNumber =
                /*monthCode +*/ nextSerial.ToString("0000000000");

            return decimal.Parse(
                documentNumber,
                System.Globalization.CultureInfo.InvariantCulture);
        }

        #region E-Invoicing 
        public static async Task<List<CygnetLegalAgency>> GetBillFromLegalAgency(string fyCode,IDapperHelper dapper,IDbTransaction? txn=null)
        {
            return new List<CygnetLegalAgency>  { new CygnetLegalAgency
                    {
                    AgencyId = "TL",
                    AgencyName = "UAE Sender",
                    PassportCountry = "AE"
                    }};
        }
        public static async Task<List<CygnetLegalAgency>> GetBillToLegalAgency(string customerID, IDapperHelper dapper, IDbTransaction? txn = null)
        {
            return new List<CygnetLegalAgency> { new CygnetLegalAgency{
                                                        AgencyId = "EID",
                                                        AgencyName = "UAE Receiver",
                                                        PassportCountry = "AE" }
                                                };
        }

        public static async Task<List<CygnetPaymentDetail>> GetPaymentDetails(string paycode, IDapperHelper dapper, IDbTransaction? txn = null)
        {
            return new List<CygnetPaymentDetail> { new CygnetPaymentDetail { PaymentMode = "10" } };
        }
        #endregion E-Invoicing
    }

}
