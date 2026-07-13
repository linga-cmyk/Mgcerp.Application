using Azure;
using Azure.Core;
using MFiles.Integration.Services;
using Mgcerp.Models.DTOModels;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens.Experimental;
using System.Collections.Generic;

namespace Mgcerp.Application.Services
{
    public class IMfileCompose
    {
        public static async Task<List<object>> ValidateFiles(
           IMFilesService mFiles,
           List<IFormFile> files)
        {
            var validationErrors = new List<object>();

            foreach (var file in files)
            {
                if (file == null)
                    continue;

                await using var stream = file.OpenReadStream();

                var result = mFiles.ValidateFile(stream, file.FileName);

                if (!result.IsValid)
                {
                    validationErrors.Add(new
                    {
                        File = file.FileName,
                        Error = result.Error
                    });
                }
            }

            return validationErrors;
        }


        public static async Task<List<object>> UploadMfiles(IMFilesService mFiles, List<IFormFile> files,string uuid,
            string TransactionName, string username)
        {
            // Build requests
            var requests = new List<MFilesUploadRequest>();
            var streams = new List<Stream>();
            List<object> fileuploadstatus = new List<object>();
            try
            {
                foreach (var file in files)
                {
                    if (file != null)
                    {
                        var stream = file.OpenReadStream();
                        streams.Add(stream);
                        requests.Add(new MFilesUploadRequest
                        {
                            FileStream = stream,
                            FileName = file.FileName,
                            ClassId = 1,
                            TransactionUniqueId = uuid,
                            NameOrTitle = file.FileName,
                            TransactionName = TransactionName?? "ERP",
                            UserId = username??"lingai",
                            UploadedDate = DateTime.Today
                        });
                    }
                }

            
                var result = await mFiles.UploadMultipleDocumentsAsync(requests);
                fileuploadstatus.Add(new
                {

                    totalRequested = result.TotalRequested,
                    totalSuccess = result.TotalSuccess,
                    totalFailed = result.TotalFailed,
                    allSucceeded = result.AllSucceeded,
                    files = result.Results.Select(r => new
                    {
                        title = r.Title,
                        success = r.Success,
                        objectId = r.Success ? r.ObjectId : 0,
                        fileSize = r.FileSizeFormatted,
                        failureReason = r.FailureReason
                    })
                });
            }
            catch(Exception ex) 
            {
                fileuploadstatus.Add(new
                {
                    Error = ex.Message,
                });
            }
            return fileuploadstatus;
        }
    }
}
