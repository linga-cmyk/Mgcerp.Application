using Azure;
using Dapper;
using MFiles.Integration.Services;
using Mgcerp.Application.Services.Impl;
using Mgcerp.Infrastructure.Helpers;
using Mgcerp.Models;
using Mgcerp.Models.Application;
using Mgcerp.Models.DTOModels.Masters;
using Microsoft.IdentityModel.Tokens.Experimental;

namespace Mgcerp.Application.Services
{
    public interface IBusinessPartnerService
    {
        Task<List<BusinessPartner>> GetBusinessPartners();
        Task<BusinessPartner> GetBusinessPartnersById(Guid id);
        Task<BusinessPartner> CreateBusinessPartnerAsync(BusinessPartner businessPartner);
        Task<int> UpdateBusinessPartnerAsync(BusinessPartner businessPartner);
        Task<int> DeleteBusinessPartnerAsync(Guid id);
        Task<int> ApproveBusinessPartnerAsync(Guid id);
        Task<int> RejectBusinessPartnerAsync(Guid id, string Remarks);
        Task<List<BusinessPartner>> SearchPartner(string keyword);
        Task<ResponseResult> CreateLedgerAccount(Guid id, string groupcode);


    }
    public class BusinessPartnerService : IBusinessPartnerService
    {
        private readonly IDapperHelper _dapper;
        private readonly IMFilesService _mFiles;
        private readonly IApplicationServices _appservice;
        public BusinessPartnerService(IDapperHelper dapper,IMFilesService mFiles, IApplicationServices appservice)
        {
            _dapper=dapper;
            _mFiles = mFiles;
            _appservice=appservice;
        }
        public async Task<int> ApproveBusinessPartnerAsync(Guid id)
        {
            const string sql = @"
                            UPDATE BusinessPartners
                            SET

                            RegistrationStatus='Approved',
                            ApprovedOn=GETDATE()

                            WHERE BusinessPartnerId=@Id";

                return await _dapper.ExecuteAsync(sql,
                new { Id = id });
        }

        public async  Task<BusinessPartner> CreateBusinessPartnerAsync(BusinessPartner businessPartner)
        {
            businessPartner.BusinessPartnerId = Guid.NewGuid();
            List<object> validationErrors = new List<object>();
            var files = new List<Microsoft.AspNetCore.Http.IFormFile>();

            if (businessPartner.Tradelicense != null) files.Add(businessPartner.Tradelicense);
            if (businessPartner.Vat!= null) files.Add(businessPartner.Vat);

            if (files.Count>0)
            {
                
                validationErrors = await IMfileCompose.ValidateFiles(_mFiles, files);
            }
            if (validationErrors.Count > 0)
            {
                string errors = string.Join("; ", validationErrors);
                return new BusinessPartner
                {
                    CompanyName = errors
                };
            }
            else
            {
                validationErrors = await IMfileCompose.UploadMfiles(_mFiles, files, businessPartner.BusinessPartnerId.ToString(), "Vendor", "lignai");
            }



               
            const string sql = @"
                    INSERT INTO BusinessPartners
                    (
                    BusinessPartnerId,PartnerType,CompanyName,CompanyNameArabic,ContactPerson,
                    AuthorizedPerson,EmailAddress,MobileNo,PhoneNo,FaxNo,
                    Website,CurrencyCode,CountryCode,TerritoryCode,Address,
                    Remarks,RegistrationStatus,CreatedOn)
                    VALUES
                    (
                    @BusinessPartnerId,@PartnerType,@CompanyName,@CompanyNameArabic,@ContactPerson,
                    @AuthorizedPerson,@EmailAddress,@MobileNo,@PhoneNo,@FaxNo,
                    @Website,@CurrencyCode,@CountryCode,@TerritoryCode,@Address,
                    @Remarks,'Pending',GETDATE()
                    )";

             await _dapper.ExecuteAsync(sql, businessPartner);
            

            return businessPartner;



        }

        public async Task<ResponseResult> CreateLedgerAccount(Guid id, string groupcode )
        {

            BusinessPartner? model = await _dapper.QuerySingleAsync<BusinessPartner>
                (@"select *from BusinessPartners where BusinessPartnerId=@id", new { id });
            if(model==null)
            {
                return new ResponseResult()
                {
                    ErrorMessage = "No Business partnerid found"
                };
            }else if(!string.IsNullOrEmpty(model.ERPAccCode))
            {
                return new ResponseResult()
                {
                    ErrorMessage = "Account already entered" + model.ERPAccCode
                };
            }


            if (string.IsNullOrWhiteSpace(model.CompanyName))
                return new ResponseResult()
                {
                    ErrorMessage = "Account already entered" + model.ERPAccCode
                };



            dynamic? accountGroup = await _dapper.QuerySingleAsync<dynamic>(
                            @"SELECT
                            dicahmas.ah_code AS AhCode,
                            dicagmas.ag_code AS AgCode,
                            dicafmas.af_code AS AfCode,
                            dicasmas.as_code AS AsCode
                        FROM dicasmas
                        INNER JOIN dicafmas
                            ON dicafmas.af_code = dicasmas.af_code
                        INNER JOIN dicagmas
                            ON dicagmas.ag_code = dicafmas.ag_code
                        INNER JOIN dicahmas
                            ON dicahmas.ah_code = dicagmas.ah_code
                        WHERE dicasmas.as_code = @groupcode;",
                        new { AsCode = groupcode });
            if(accountGroup==null)
                return new ResponseResult()
                {
                    ErrorMessage = "Account already entered" + model.ERPAccCode
                };

            string ahCode = accountGroup.ah_code;
            string agCode = accountGroup.ag_code;
            string afCode = accountGroup.af_code;
            string asCode = accountGroup.as_code;
            string alphaCode = model.CompanyName.Trim()[0].ToString().ToUpper();
             
            const string getNextAccountNumsql = @"
                    SELECT 
                        @Prefix + @AlphaCode +
                        RIGHT('0000' +
                            CAST(
                                ISNULL(MAX(Convert(varchar(4),RIGHT(acc_code,4) )),0) + 1
                            AS VARCHAR(4)),4) as MaxSerial
                    FROM dicadmas
                    WHERE   as_code  = @Prefix and alpha_code = @AlphaCode
                      AND acc_code LIKE @Prefix + @AlphaCode + '%';";
            var accgrouparg = new DynamicParameters(new
            {
                AlphaCode= alphaCode,
                Prefix= groupcode
            });

            const string insertaccount = @"INSERT INTO dicadmas
            (
                fy_code,ah_code,ag_code,as_code,alpha_code,acc_code,
                acc_name,open_date,credit_period,credit_limit,ytd_sign,
                ytd_bal,close_int,cur_code,report_acc_code,sub_head,accounts_category,
                af_code,is_parent,accounts_category_2,own_activity,all_company,
                expense_cat,df_delivery_term_mode,IsDisplayMobile,uuid,electronic_id
            ) VALUES
            (  @fy_code,@ah_code,@ag_code,@as_code,@alpha_code,
                @acc_code,@acc_name,@open_date,@credit_period,@credit_limit,@ytd_sign,
                @ytd_bal,@close_int,@cur_code,@report_acc_code,@sub_head,
                @accounts_category,@af_code,@is_parent,@accounts_category_2,@own_activity,
                @all_company,@expense_cat,@df_delivery_term_mode,@IsDisplayMobile,@uuid,@eid
            );";
            var args = new DynamicParameters();

            args.Add("@fy_code", "10");
            args.Add("@ah_code", ahCode);
            args.Add("@ag_code", agCode);
            args.Add("@as_code", groupcode);
            args.Add("@alpha_code", alphaCode);
            args.Add("@acc_name", model.CompanyName);
            args.Add("@open_date", DateTime.Now);
            args.Add("@credit_period", 0);
            args.Add("@credit_limit", 0m);
            args.Add("@ytd_sign", 0);
            args.Add("@ytd_bal", 0m);
            args.Add("@close_int", "N");
            args.Add("@cur_code",model.CurrencyCode);
            args.Add("@report_acc_code", string.Empty);
            args.Add("@sub_head", "N");
            args.Add("@accounts_category", string.Empty);
            args.Add("@af_code", afCode);
            args.Add("@is_parent", "N");
            args.Add("@accounts_category_2", string.Empty);
            args.Add("@own_activity", model.OwnActivity);
            args.Add("@all_company", "Y");
            args.Add("@expense_cat", "");
            args.Add("@df_delivery_term_mode", string.Empty);
            args.Add("@IsDisplayMobile", "N");
            args.Add("@uuid", Guid.NewGuid().ToString());
            args.Add("@eid",  model.ElectronicId?? "0000:0000000000");
            var batch = new SqlTransactionBatch();
             

            batch.AddScalar(getNextAccountNumsql, "AccCode", accgrouparg);
            batch.AddExecute(
                        insertaccount,
                        ctx =>  
                        {
                            args.Add("@acc_code", ctx.Get<string>("AccCode"));
                            return args;
                        });


            var updateaccount = @"update BusinessPartners set ERPAccCode =@ERPAccCode where BusinessPartnerId = @uuid";
            var updateaccountargs = new DynamicParameters();
            updateaccountargs.Add("@uuid",id);
            batch.AddExecute(updateaccount, ctx =>
            {
                updateaccountargs.Add("@ERPAccCode", ctx.Get<string>("AccCode"));
                return updateaccountargs;
            });

            var result = await _appservice.ExecuteBulkAsync(batch);
            if(result.IsValid)
            {
                model.ERPAccCode = (string?)result.Data;
                result.Data = model;
            }
            return result;

            
        }

        public async Task<int> DeleteBusinessPartnerAsync(Guid id)
        {
            const string sql = @"
                    DELETE BusinessPartners
                    WHERE BusinessPartnerId=@Id";

            var rows = await _dapper.ExecuteAsync(sql,
                new { Id = id });

            return rows;

        }

        public async Task<List<BusinessPartner>> GetBusinessPartners()
        {
            const string sql = @"
                    SELECT *
                    FROM BusinessPartners
                    ORDER BY CompanyName";

            return (await _dapper.QueryAsync<BusinessPartner>(sql)).ToList();
        }

        public async Task<BusinessPartner> GetBusinessPartnersById(Guid id)
        {
            const string sql = @" SELECT * FROM BusinessPartners WHERE BusinessPartnerId=@Id";
            var partner = await _dapper.QuerySingleAsync<BusinessPartner>(sql, new { Id = id });
            if(partner == null) 
                return new BusinessPartner();
            return partner;
             
        }

        public async Task<int> RejectBusinessPartnerAsync(Guid id, string Remarks)
        {
            const string sql = @"
                UPDATE BusinessPartners
                SET

                RegistrationStatus='Rejected',
                RejectedReason=@remarks,
                RejectedOn=GETDATE()

                WHERE BusinessPartnerId=@Id";

            return await _dapper.ExecuteAsync(sql,new { Id = id, Remarks });

            
        }

        public async Task<List<BusinessPartner>> SearchPartner(string keyword)
        {
            const string sql = @"
                    SELECT *
                    FROM BusinessPartners
                    WHERE

                    CompanyName LIKE '%' + @keyword + '%'
                    OR
                    EmailAddress LIKE '%' + @keyword + '%'
                    OR
                    MobileNo LIKE '%' + @keyword + '%'

                    ORDER BY CompanyName";

            var data = await _dapper.QueryAsync<BusinessPartner>(
                sql,
                new { keyword });
            return data.ToList();
        }

        public async Task<int> UpdateBusinessPartnerAsync(BusinessPartner businessPartner)
        {
         

            const string sql = @"
                        UPDATE BusinessPartners
                        SET

                        PartnerType=@PartnerType,
                        CompanyName=@CompanyName,
                        CompanyNameArabic=@CompanyNameArabic,
                        ContactPerson=@ContactPerson,
                        AuthorizedPerson=@AuthorizedPerson,
                        EmailAddress=@EmailAddress,
                        MobileNo=@MobileNo,
                        PhoneNo=@PhoneNo,
                        FaxNo=@FaxNo,
                        Website=@Website,
                        CurrencyCode=@CurrencyCode,
                        CountryCode=@CountryCode,
                        TerritoryCode=@TerritoryCode,
                        Address=@Address,
                        Remarks=@Remarks,
                        ModifiedOn=GETDATE()

                        WHERE BusinessPartnerId=@BusinessPartnerId";

            return await _dapper.ExecuteAsync(sql, businessPartner);

             
        }
    }
}
