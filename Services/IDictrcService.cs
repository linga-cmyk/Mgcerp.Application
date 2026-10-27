using Mgcerp.Infrastructure.Helpers;
using Mgcerp.Models.DTOModels.Masters;
using Microsoft.EntityFrameworkCore.Query;

namespace Mgcerp.Application.Services
{
    public interface IDictrcService
    {
        Task<IEnumerable<DictrcDto>> GetAllAsync();

        Task<DictrcDto?> GetByCodeAsync(string trcCode);

        Task<bool> CreateAsync(DictrcDto dto);

        Task<bool> UpdateAsync(string trcCode, DictrcDto dto);

        Task<bool> DeleteAsync(string trcCode);
        Task<Dictionary<string, Dictionary<string, string>>> GetLookupValues();
    }
    public sealed class DictrcService : IDictrcService
    {
        private readonly IDapperHelper _db;

        public DictrcService(IDapperHelper db)
        {
            _db = db;
        }

        private const string SelectSql = """
        SELECT
            trc_object1 AS TrcObject1,
            fy_code AS FyCode,
            trc_code AS TrcCode,
            trc_name AS TrcName,
            trc_abr AS TrcAbr,
            vr_no AS VrNo,
            top_A_code AS TopACode,
            bot_A_code AS BotACode,
            bot_A_dbcr AS BotADbcr,
            auth1_name AS Auth1Name,
            auth2_name AS Auth2Name,
            auth3_name AS Auth3Name,
            eff_cost AS EffCost,
            inv_int AS InvInt,
            auto_verify AS AutoVerify,
            invoice_type_code AS InvoiceTypeCode,
            invoice_type_name AS InvoiceTypeName,
            einv_supply_type AS EinvSupplyType,
            einv_doc_type AS EinvDocType,
            einv_tran_type AS EinvTranType
        FROM dbo.dictrc 
        """;

        // GET ALL
        public async Task<IEnumerable<DictrcDto>> GetAllAsync()
        {
            return await _db.QueryAsync<DictrcDto>(
                SelectSql + " ORDER BY trc_code");
        }

        // GET BY PRIMARY KEY
        public async Task<DictrcDto?> GetByCodeAsync(string trcCode)
        {
            const string sql = SelectSql + """
            WHERE trc_code = @TrcCode
            """;

            return await _db.QuerySingleAsync<DictrcDto>(
                sql,
                new { TrcCode = trcCode });
        }

        // CREATE
        public async Task<bool> CreateAsync(DictrcDto dto)
        {
            const string sql = """
            INSERT INTO dbo.dictrc
            (
                fy_code,
                trc_code,
                trc_name,
                trc_object1,
                trc_abr,
                vr_no,
                top_A_code,
                bot_A_code,
                bot_A_dbcr,
                auth1_name,
                auth2_name,
                auth3_name,
                eff_cost,
                inv_int,
                auto_verify,
                invoice_type_code,
                invoice_type_name,
                einv_supply_type,
                einv_doc_type,
                einv_tran_type
            )
            VALUES
            (
                @FyCode,
                @TrcCode,
                @TrcName,
                @TrcObject1,
                @TrcAbr,
                @VrNo,
                @TopACode,
                @BotACode,
                @BotADbcr,
                @Auth1Name,
                @Auth2Name,
                @Auth3Name,
                @EffCost,
                @InvInt,
                @AutoVerify,
                @InvoiceTypeCode,
                @InvoiceTypeName,
                @EinvSupplyType,
                @EinvDocType,
                @EinvTranType
            );
            """;

            var rows = await _db.ExecuteAsync(sql, dto);

            return rows > 0;
        }

        // UPDATE
        public async Task<bool> UpdateAsync(
            string trcCode,
            DictrcDto dto)
        {
            const string sql = """
            UPDATE dbo.dictrc
            SET
                fy_code = @FyCode,
                trc_name = @TrcName,
                trc_object1 = @TrcObject1,
                trc_abr = @TrcAbr,
                vr_no = @VrNo,
                top_A_code = @TopACode,
                bot_A_code = @BotACode,
                bot_A_dbcr = @BotADbcr,
                auth1_name = @Auth1Name,
                auth2_name = @Auth2Name,
                auth3_name = @Auth3Name,
                eff_cost = @EffCost,
                inv_int = @InvInt,
                auto_verify = @AutoVerify,
                invoice_type_code = @InvoiceTypeCode,
                invoice_type_name = @InvoiceTypeName,
                einv_supply_type = @EinvSupplyType,
                einv_doc_type = @EinvDocType,
                einv_tran_type = @EinvTranType
            WHERE trc_code = @TrcCode;
            """;

            var parameters = new
            {
                dto.FyCode,
                dto.TrcName,
                dto.TrcObject1,
                dto.TrcAbr,
                dto.VrNo,
                dto.TopACode,
                dto.BotACode,
                dto.BotADbcr,
                dto.Auth1Name,
                dto.Auth2Name,
                dto.Auth3Name,
                dto.EffCost,
                dto.InvInt,
                dto.AutoVerify,
                dto.InvoiceTypeCode,
                dto.InvoiceTypeName,
                dto.EinvSupplyType,
                dto.EinvDocType,
                dto.EinvTranType,
                TrcCode = trcCode
            };

            var rows = await _db.ExecuteAsync(sql, parameters);

            return rows > 0;
        }

        // DELETE
        public async Task<bool> DeleteAsync(string trcCode)
        {
            const string sql = """
            DELETE FROM dbo.dictrc
            WHERE trc_code = @TrcCode;
            """;

            var rows = await _db.ExecuteAsync(
                sql,
                new { TrcCode = trcCode });

            return rows > 0;
        }

        public async Task<Dictionary<string, Dictionary<string, string>>> GetLookupValues()
        {
            const string sql = @" select Code, Description from einvoiceMaster where sectionnameslno=5 and Isactive='Y' order by Code;
                                  select Code, Description from einvoiceMaster where sectionnameslno=6 and Isactive='Y' order by Code;
                                  select Code, Description from einvoiceMaster where sectionnameslno=3 and Isactive='Y' order by Code;
                                  select Code, Description from einvoiceMaster where sectionnameslno=27 and Isactive='Y' order by Code;
                                  select Code, Description from einvoiceMaster where sectionnameslno=4 and Isactive='Y' order by Code;
                            ";
            var result = await _db.QueryMultipleAsync(sql, null);
            var lookupValues = new Dictionary<string, Dictionary<string, string>>();

            lookupValues["5"] = (await result.ReadAsync<LookupDto>())
    .GroupBy(x => x.Code)
    .ToDictionary(g => g.Key, g => g.First().Description);

            lookupValues["6"] = (await result.ReadAsync<LookupDto>())
                .GroupBy(x => x.Code)
                .ToDictionary(g => g.Key, g => g.First().Description);

            lookupValues["3"] = (await result.ReadAsync<LookupDto>())
                .GroupBy(x => x.Code)
                .ToDictionary(g => g.Key, g => g.First().Description);

            lookupValues["27"] = (await result.ReadAsync<LookupDto>())
                .GroupBy(x => x.Code)
                .ToDictionary(g => g.Key, g => g.First().Description);

            lookupValues["4"] = (await result.ReadAsync<LookupDto>())
                .GroupBy(x => x.Code)
                .ToDictionary(g => g.Key, g => g.First().Description);

            return lookupValues;
             
        }
    }
    public class LookupDto
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
