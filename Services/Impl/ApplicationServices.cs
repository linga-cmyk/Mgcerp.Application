
using Mgcerp.Infrastructure.Helpers;
using Mgcerp.Models;
using Mgcerp.Models.Application;
using System.Data;

namespace Mgcerp.Application.Services.Impl
{
    public class ApplicationServices : IApplicationServices
    {
        private readonly IDapperHelper _dapper;
        public ApplicationServices(IDapperHelper dapper)
        {
                _dapper = dapper;
        }

        public async Task<ResponseResult> ExecuteBulkAsync(IEnumerable<SqlExecutionItem> items, string? countryCode = null)
        {
            // ─── Read Example in DapperHelper ────────────────────────────────────────────────────────────────
            /*
             * var batch = new SqlTransactionBatch()
                        .AddScalar(
                            sqlGetNextAccCode,
                            "AccCode",
                            new
                            {
                                Prefix = "120201",
                                AlphaCode = "A"
                            })

                        .AddExecute(
                            insertHeader,
                            ctx => new
                            {
                                AccCode = ctx["AccCode"],
                                Name = "ABC Company"
                            })

                        .AddExecute(
                            insertContact,
                            ctx => new
                            {
                                AccCode = ctx["AccCode"],
                                Contact = "John"
                            });

                    await _applicationService.ExecuteBulkAsync(batch);

             var batch = new SqlBatch();
                batch.Add(insertHeader, header);
                batch.Add(insertDetail, details);
                batch.Add(updateStock, stock);
                batch.Add(updateLedger);
                await _applicationService.ExecuteBulkAsync(batch);
             
             */
            ResponseResult result = new ResponseResult();
            try
            {
                return await _dapper.ExecuteBulkAsync(items, countryCode);
            }
            catch (Exception ex)
            {
                return new ResponseResult
                {
                    IsValid = false,
                    ErrorMessage = ex.Message,
                };
            }
        }

        public async Task<ResponseResult> ExecuteSqlNonQuery(string sql, object? param = null, IDbTransaction? transaction = null, string? countryCode = null)
        {
            try
            {
                int response =  await _dapper.ExecuteTransactionAsync(sql,param,transaction,countryCode);
                return new ResponseResult
                {
                    IsValid = true,
                    SuccessMessage = response.ToString(),
                };
            }
            catch (Exception ex)
            {
                return new ResponseResult
                {
                    IsValid = false,
                    ErrorMessage = ex.Message,
                };

            }
        }

       

        public async Task<T?> ExecuteSqlScalar<T>(string sql, object? param = null, IDbTransaction? transaction = null, string? countryCode = null)
        {
            try
            {
                return await _dapper.ExecuteScalarAsync<T>(sql, param, transaction, countryCode);
                 
            }
            catch (Exception)
            {
                return default;

            }
        }

        public async Task<T?> ExecuteSqlSingle<T>(string sql, object? param = null, IDbTransaction? transaction = null, string? countryCode = null)
        {
            try
            {
                return await _dapper.QuerySingleAsync<T>(sql, param, transaction, countryCode);
                 
            }
            catch (Exception)
            {
                return  default;

            }
        }
    }
    public class SqlBatch : List<SqlExecutionItem>
    {
        public void Add(string sql, object? param = null)
        {
            base.Add(new SqlExecutionItem
            {
                Sql = sql,
                Parameters = param
            });
        }
    }
    public static class ContextExtensions
    {
        public static T Get<T>(this IDictionary<string, object> ctx, string key)
        {
            return (T)ctx[key];
        }
    }
}
