using CygnetEInvoice.Models;
using Mgcerp.Application.Services;
using Mgcerp.Infrastructure.Helpers;
using Mgcerp.Models.DTOModels.EInvoice;
using Mgcerp.Models.DTOModels.EInvoice.Dtos;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;

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
        public static void SetUATVALUES(ref CygnetDocument doc)
        {
            #region Constants Value for testing
            // ---- Staging test overrides (remove when real mapping is in place) ----
            doc.EntityTaxRegNo = "100200550040003";
            doc.EntityTin = "1002005500";
            doc.EntityName = "UAE Sender";
            doc.BillFromLegalRegNo = "0235-1002005500";
            doc.BillFromLegalName = "UAE Sender";
            doc.BillFromEndpoint = "0235:1002005500";
            doc.BillToTaxRegNo = "100200660040003";
            doc.BillToLegalName = "UAE Receiver";
            doc.BillToEndpoint = "0235:1002006600";
            doc.DocumentNumber = string.Concat(doc.DocumentNumber, DateTime.Now.ToString("yyyyMMddHHmmss"));
            doc.DocumentDate = DateTime.Now.ToString("yyyy-MM-dd");
            doc.BillFromCountrySubdivision = "SHJ";
            doc.BillToLegalRegNo = "0235-1100200660040003";
            doc.BillToEndpoint = "0235:1002006600";
            doc.BillToIdentifier = "0027-2354";
            doc.BillToCountrySubdivision = "SHJ";
            // 480 / 81: supplier identified by TIN, not TRN (staging rule)
            if (doc.DocumentTypePurpose is "480" or "81")
            {
                doc.BillFromTaxRegNo = null;
                doc.BillFromAddSchIdDetails = new() { new CygnetSchemeId { Scheme = "VAT", Id = doc.EntityTin } };
            }
            else
            {
                doc.BillFromTaxRegNo = "100200550040003";
            }

             doc.BillFromLegalAgency= new List<CygnetLegalAgency>
                        { new CygnetLegalAgency
                        {
                        AgencyId = "TL",
                        AgencyName = "UAE Sender",
                        PassportCountry = "AE"
                        }};

            doc.BillToLegalAgency =   new List<CygnetLegalAgency> { new CygnetLegalAgency{
                                                        AgencyId = "EID",
                                                        AgencyName = "UAE Receiver",
                                                        PassportCountry = "AE" }
                                                };
            #endregion Constants
        }
        public static async Task<EInvoiceBillAddress> GetBillAddress(string AccCode, IDapperHelper dapper, bool IsBillAddressForBuyer=false )
        {
            string sql = string.Empty;
            if (!IsBillAddressForBuyer) //seller
            {
                  sql = @"SELECT
                                    COUNTRY_MASTER.alphacode2 AS Country,
                                    fymas.VAT_REG_NO AS EntityTaxRegNo,
                                    fymas.licence_no AS EntityTin,
                                    fymas.company_name AS EntityName, 
                                    fymas.electronic_id as Identifier,
                                    'PEDEL' AS Purpose,
                                    'PEDEL' AS AutoPushOrGenerate,
                                    fymas.VAT_REG_NO AS TaxRegNo,
                                    Replace(fymas.electronic_id,':','-') AS LegalRegNo,
                                    fymas.Company_Name AS LegalName,
                                    fymas.electronic_id AS Endpoint,
                                    fymas.fy_add1 AS StreetName,
                                    fymas.city AS City,
                                    fymas.Postal_Code AS PostCode,
                                    COUNTRY_MASTER.alphacode2 AS BillCountry,
                                    fymas.provincestate AS CountrySubdivision
                                FROM fymas
                                INNER JOIN COUNTRY_MASTER
                                    ON COUNTRY_MASTER.COUNTRY_CODE = fymas.country_code
                                    WHERE fymas.fy_code = @AccCode;";
            }
            else  //Buyer
            {
                    sql = @" SELECT
                                extercust.VAT_REG_NO AS TaxRegNo,
                                extercust.acc_name AS LegalName,
                                 '0235-'+  trade_lic_no AS Identifier,
                                '0235-' + trade_lic_no AS LegalRegNo,
                                extercust.electronic_id AS Endpoint,
                                ISNULL(extercust.Address_Street, 'N/A') AS StreetName,
                                ISNULL(extercust.city, 'N/A') AS City,
                                extercust.Postal_Code AS PostCode,
                                cust_country.alphacode2 AS Country,
                                extercust.provincestate AS CountrySubdivision
                            FROM dicadmas extercust
                            INNER JOIN COUNTRY_MASTER cust_country
                                ON cust_country.COUNTRY_CODE = extercust.country_code
                            WHERE extercust.acc_code = @AccCode;";
            }
            var result = await dapper.QuerySingleAsync<EInvoiceBillAddress>(sql, new { AccCode });
            return result == null ? new EInvoiceBillAddress() : result;
        }
        public static void GetSellerDetails(
                  ref CygnetDocument doc,
                  string co,
                  IDapperHelper dapper,
                  IDbTransaction? txn = null)
        {
            const string sql = @"
                        SELECT
                            COUNTRY_MASTER.alphacode2 AS Country,
                            fymas.VAT_REG_NO AS EntityTaxRegNo,
                            fymas.licence_no AS EntityTin,
                            fymas.company_name AS EntityName,
                            'PEDEL' AS Purpose,
                            'PEDEL' AS AutoPushOrGenerate,
                            fymas.VAT_REG_NO AS BillFromTaxRegNo,
                            fymas.electronic_id AS BillFromLegalRegNo,
                            fymas.Company_Name AS BillFromLegalName,
                            fymas.electronic_id AS BillFromEndpoint,
                            fymas.fy_add1 AS BillFromStreetName,
                            fymas.city AS BillFromCity,
                            fymas.Postal_Code AS BillFromPostCode,
                            COUNTRY_MASTER.alphacode2 AS BillFromCountry,
                            fymas.provincestate AS BillFromCountrySubdivision
                        FROM fymas
                        INNER JOIN COUNTRY_MASTER
                            ON COUNTRY_MASTER.COUNTRY_CODE = fymas.country_code
                        WHERE fymas.fy_code = @co;";

            var seller = dapper
                .QueryAsync<CygnetDocument>(sql, new { co })
                .GetAwaiter()
                .GetResult()
                .FirstOrDefault();

            if (seller == null)
            {
                throw new InvalidOperationException(
                    $"Seller details not found for company '{co}'.");
            }

            doc.Country = seller.Country;
            doc.EntityTaxRegNo = seller.EntityTaxRegNo;
            doc.EntityTin = seller.EntityTin;
            doc.EntityName = seller.EntityName;
            doc.Purpose = seller.Purpose;
            doc.AutoPushOrGenerate = seller.AutoPushOrGenerate;
            doc.BillFromTaxRegNo = seller.BillFromTaxRegNo;
            doc.BillFromLegalRegNo = seller.BillFromLegalRegNo;
            doc.BillFromLegalName = seller.BillFromLegalName;
            doc.BillFromEndpoint = seller.BillFromEndpoint;
            doc.BillFromStreetName = seller.BillFromStreetName;
            doc.BillFromCity = seller.BillFromCity;
            doc.BillFromPostCode = seller.BillFromPostCode;
            doc.BillFromCountry = seller.BillFromCountry;
            doc.BillFromCountrySubdivision = seller.BillFromCountrySubdivision;
        }

        public static void GetExternalBuyerDetails(
                  ref CygnetDocument docBuy,
                  string acccode,
                  IDapperHelper dapper,
                  IDbTransaction? txn = null)
        {
            const string sql = @"
                        select extercust.VAT_REG_NO AS BillToTaxRegNo,
                                 extercust.acc_name AS BillToLegalName,
                                 extercust.VAT_REG_NO  AS BillToLegalRegNo,   
                                extercust.trade_lic_no AS BillToEndpoint,
                                extercust.trade_lic_no AS BillToIdentifier,
                                Isnull(extercust.Address_Street,'N/A') AS BillToStreetName,
                                Isnull(extercust.city,'N/A') AS BillToCity, 
                                extercust.Postal_Code AS BillToPostCode ,
                                cust_country.alphacode2 AS BillToCountry ,
                                extercust. provincestate AS BillToCountrySubdivision
                            from dicadmas extercust 
                            inner join COUNTRY_MASTER cust_country on cust_country.COUNTRY_CODE = extercust.country_code
                        WHERE extercust.acc_code= @acccode;";

            var Buyer = dapper
                .QueryAsync<CygnetDocument>(sql, new { acccode })
                .GetAwaiter()
                .GetResult()
                .FirstOrDefault();

            if (Buyer == null)
            {
                throw new InvalidOperationException(
                    $"Buyer details not found for company '{acccode}'.");
            }

            docBuy.BillToTaxRegNo = Buyer.BillToTaxRegNo;
            docBuy.BillToLegalName = Buyer.BillToLegalName;
            docBuy.BillToLegalRegNo = Buyer.BillToLegalRegNo;
            docBuy.BillToEndpoint = Buyer.BillToEndpoint;
            docBuy.BillToIdentifier = Buyer.BillToIdentifier;
            docBuy.BillToStreetName = Buyer.BillToStreetName;
            docBuy.BillToCity = Buyer.BillToCity;
            docBuy.BillToPostCode = Buyer.BillToPostCode;
            docBuy.BillToCountry = Buyer.BillToCountry;
            docBuy.BillToCountrySubdivision = Buyer.BillToCountrySubdivision;

        }


        public static void GetInternalBuyerDetails(
                   ref CygnetDocument docBuy,
                   string co,
                   IDapperHelper dapper,
                   IDbTransaction? txn = null)
        {
            const string sql = @"
                        SELECT
                            
                            fymas.VAT_REG_NO AS BillToTaxRegNo,
                            fymas.electronic_id AS BillToLegalRegNo,
                            fymas.Company_Name AS BillToLegalName,
                            fymas.electronic_id AS BillToEndpoint,
                            fymas.fy_add1 AS BillToStreetName,
                            fymas.city AS BillToCity,
                            fymas.Postal_Code AS BillToPostCode,
                            COUNTRY_MASTER.alphacode2 AS BillToCountry,
                            fymas.provincestate AS BillToCountrySubdivision
                        FROM fymas
                        INNER JOIN COUNTRY_MASTER
                            ON COUNTRY_MASTER.COUNTRY_CODE = fymas.country_code
                        WHERE fymas.fy_code = @co;";

            var Buyer = dapper
                .QueryAsync<CygnetDocument>(sql, new { co })
                .GetAwaiter()
                .GetResult()
                .FirstOrDefault();

            if (Buyer == null)
            {
                throw new InvalidOperationException(
                    $"Seller details not found for company '{co}'.");
            }

            docBuy.BillToTaxRegNo = Buyer.BillToTaxRegNo;
            docBuy.BillToLegalName = Buyer.BillToLegalName;
            docBuy.BillToLegalRegNo = Buyer.BillToLegalRegNo;
            docBuy.BillToEndpoint = Buyer.BillToEndpoint;
            docBuy.BillToIdentifier = "Buyer.BillToIdentifier";
            docBuy.BillToStreetName = Buyer.BillToStreetName;
            docBuy.BillToCity = Buyer.BillToCity;
            docBuy.BillToPostCode = Buyer.BillToPostCode;
            docBuy.BillToCountry = Buyer.BillToCountry;
            docBuy.BillToCountrySubdivision = Buyer.BillToCountrySubdivision;
        }

        public static async Task<List<CygnetLegalAgency>> GetBillFromLegalAgency(string fyCode, IDapperHelper dapper, IDbTransaction? txn = null)
        {
            const string sql = @"Select 
                                    'TL' as AgencyId, 
                                    Isnull(TradeLicenseIssueAuthority.Description,'') As AgencyName, 
                                    'AE' as PassportCountry 
                                from 
                                TradeLicenseIssueAuthority
                                inner join fymas on fymas.Tl_IssueAuthCode = TradeLicenseIssueAuthority.code
                                where fymas.fy_code= @fyCode";
            var record = await dapper.QuerySingleAsync<CygnetLegalAgency>(sql, new { fyCode });
            if (record == null)
            {
                return new List<CygnetLegalAgency>();
            }
            var agencyList = new List<CygnetLegalAgency>();
            agencyList.Add(record);
            return agencyList;
                //return new List<CygnetLegalAgency>  
                //        { new CygnetLegalAgency
                //        {
                //        AgencyId = "TL",
                //        AgencyName = "UAE Sender",
                //        PassportCountry = "AE"
                //        }};
        }
        public static async Task<List<CygnetLegalAgency>> GetBillToLegalAgency(string customerID, IDapperHelper dapper, IDbTransaction? txn = null)
        {
          const string sql=@"Select AgencyId='TL', Isnull(TradeLicenseIssueAuthority.Description,'') As AgencyName, PassportCountry='AE' from 
                                TradeLicenseIssueAuthority
                                inner join dicadmas on dicadmas.Tl_IssueAuthCode = TradeLicenseIssueAuthority.code
                                where dicadmas.acc_code =@customerID";


            var record = await dapper.QuerySingleAsync<CygnetLegalAgency>(sql, new { customerID });
            if (record == null)
            {
                return new List<CygnetLegalAgency>();
            }
            var agencyList = new List<CygnetLegalAgency>();
            agencyList.Add(record);
            return agencyList;
            //return new List<CygnetLegalAgency> { new CygnetLegalAgency{
            //                                            AgencyId = "EID",
            //                                            AgencyName = "UAE Receiver",
            //                                            PassportCountry = "AE" }
            //                                    };
        }

        public static async Task<List<CygnetPaymentDetail>> GetPaymentDetails(string paycode, IDapperHelper dapper, IDbTransaction? txn = null)
        {
            return new List<CygnetPaymentDetail> { new CygnetPaymentDetail { PaymentMode = "10" } };
        }
        public static async Task<List<CygnetVatCategoryTotal>> GetVatCategoryWiseTotal(  List<CygnetLineItem> lineItems)
        {
            if (lineItems == null || lineItems.Count == 0)
            {
                return new List<CygnetVatCategoryTotal>();
            }
            var categoryTotal = lineItems
                        .GroupBy(x => x.TaxCategoryCode)
                        .Select(group =>
                        {
                            decimal taxableAmount = group.Sum(x =>
                                decimal.TryParse(
                                    x.TaxableAmount,
                                    NumberStyles.Any,
                                    CultureInfo.InvariantCulture,
                                    out decimal taxable)
                                    ? taxable
                                    : 0m);

                            decimal taxAmount = group.Sum(x =>
                                decimal.TryParse(
                                    x.TaxAmount,
                                    NumberStyles.Any,
                                    CultureInfo.InvariantCulture,
                                    out decimal tax)
                                    ? tax
                                    : 0m);

                            string? taxRate = group
                                .Select(x => x.TaxRate)
                                .FirstOrDefault(rate => !string.IsNullOrWhiteSpace(rate));

                            string? exemptionReason = group
                                .Select(x => x.TaxExemptionReasonCode)
                                .FirstOrDefault(reason => !string.IsNullOrWhiteSpace(reason));

                            return new CygnetVatCategoryTotal
                            {
                                VatCategoryCode = group.Key,
                                VatCategoryRate = taxRate,
                                VatCategoryTaxableAmount = taxableAmount.ToString(
                                    "0.00", CultureInfo.InvariantCulture),
                                VatCategoryTaxAmount = taxAmount.ToString(
                                    "0.00", CultureInfo.InvariantCulture),
                                VatExemptionReason = exemptionReason
                            };
                        })
                        .ToList();
            return categoryTotal;
        }
        public static async Task<List<EInvoiceSubmissionDto>?> GetInvoiceSubmissionLog(string uuid, IDapperHelper dapper, IDbTransaction? txn=null)
        {
            const string sql = @"
            SELECT
                Id,
                Country_Code,
                ErpDocumentNumber,
                SubmittedDocumentNumber,
                DocumentUuid,
                SupplyType,
                DocumentType,
                DocumentTypePurpose,
                TransactionTypeCode,
                DocumentDate,
                DocumentCurrency,
                ExchangeRate,
                TotalDocumentAmount,
                TotalDocAmountInAccCurrency,
                EntityTaxRegNo,
                BillToTaxRegNo,
                BillToLegalName,
                ReferenceId,
                Status,
                StatusText,
                ErrorMessages,
                StatusAttempts,
                NextCheckAt,
                LastCheckedAt,
                CompletedAt,
                LastStatusHttpCode,
                LastStatusResponse,
                SubmitHttpCode,
                SubmitResponse,
                SubmittedAt,
                SubmittedBy,
                UpdatedAt
            FROM dbo.einvoiceSubmission
            WHERE DocumentUuid = @DocumentUuid;";

            List<EInvoiceSubmissionDto>? result = (await dapper.QueryAsync<EInvoiceSubmissionDto>(sql,new { DocumentUuid = uuid.Trim() })).ToList();

            return result;
        }
        #endregion E-Invoicing
    }

}






