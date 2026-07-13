using Azure;
using MFiles.Integration.Services;
using Mgcerp.Infrastructure.Helpers;
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
         


    }
    public class BusinessPartnerService : IBusinessPartnerService
    {
        private readonly IDapperHelper _dapper;
        private readonly IMFilesService _mFiles;

        public BusinessPartnerService(IDapperHelper dapper,IMFilesService mFiles)
        {
            _dapper=dapper;
            _mFiles = mFiles;
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
