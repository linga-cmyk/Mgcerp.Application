 
using Mgcerp.Models;
using Mgcerp.Models.Application;
using System.Data;
 
namespace Mgcerp.Application.Services
{
    public interface IApplicationServices
    {
        // ─── Appication Level Transaction. Purpose: Get MaxVrno, Insert/Update Header, details, subdetails With transaction ────────────────────────────────────────────────────────────────
        Task<ResponseResult> ExecuteSqlNonQuery(string sql, object? param = null, IDbTransaction? transaction = null, string? countryCode = null);
        Task<T?> ExecuteSqlScalar<T>(string sql, object? param = null, IDbTransaction? transaction = null, string? countryCode = null);
        Task<T?> ExecuteSqlSingle<T>(string sql, object? param = null, IDbTransaction? transaction = null, string? countryCode = null);
        Task<ResponseResult> ExecuteBulkAsync(IEnumerable<SqlExecutionItem> items,string? countryCode = null);
    }
}
