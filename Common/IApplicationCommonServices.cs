using Mgcerp.Infrastructure.Helpers;
using Mgcerp.Models.DTOModels.EInvoice;
using System.Data;

namespace Mgcerp.Application.Common
{
    public interface IApplicationCommonServices
    {
        Task<decimal> GetNewDocumentNumber(
            string Co,
            DateTime DocumentDate,
            string TransactionCode,
            IDapperHelper dapper,
            IDbTransaction? txn = null);


        void GetSellerDetails(
       ref CygnetDocument doc,
       string co,
       IDapperHelper dapper,
       IDbTransaction? txn = null);

        void GetExternalBuyerDetails(
       ref CygnetDocument docBuy,
       string acccode,
       IDapperHelper dapper,
       IDbTransaction? txn = null);

        void GetInternalBuyerDetails(
       ref CygnetDocument doc,
       string co,
       IDapperHelper dapper,
       IDbTransaction? txn = null);
    }
    public class ApplicationCommonServices : IApplicationCommonServices
    {
        public async Task<decimal> GetNewDocumentNumber(
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
                monthCode + nextSerial.ToString("0000");

            return decimal.Parse(
                documentNumber,
                System.Globalization.CultureInfo.InvariantCulture);
        }

        public void GetSellerDetails(
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

        public void GetExternalBuyerDetails(
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
                    $"Seller details not found for company '{acccode}'.");
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


        public void GetInternalBuyerDetails(
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

    }
}
