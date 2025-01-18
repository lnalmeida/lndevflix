using System;
using System.Threading.Tasks;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace serverless
{
    public static class PostToStorage
    {
        [FunctionName("PostToStorage")]
        [FixedDelayRetry(3, "00:00:30")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger logger)
        {
            logger.LogInformation("Uploading media file.");

            if(!req.Headers.TryGetValue("fileTypeHeader", out var fileTypeHeader))
            {
                return new BadRequestObjectResult("Please provide a fileType header");
            }

            var fileType = fileTypeHeader.ToString();
            var formData = await req.ReadFormAsync();
            var file = formData.Files["file"];
            
            if (file == null || file.Length == 0)
            {
                return new BadRequestObjectResult("Please upload a file");
            }
            
            const long maxFileSize = 100 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                return new BadRequestObjectResult($"The file is to large, exceeding the max size of {maxFileSize / 1024 / 1024}MB");
            }

           
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            var containerName = fileType;
            var fileName = file.FileName;
            BlobClient blobClient = new BlobClient(connectionString, containerName, fileName);
            BlobContainerClient containerClient = new BlobContainerClient(connectionString, containerName);
            await containerClient.CreateIfNotExistsAsync();
            await containerClient.SetAccessPolicyAsync(PublicAccessType.BlobContainer);
            
            string blobName = fileName;
            var blob = containerClient.GetBlobClient(blobName);
            
            var uploadOptions = new BlobUploadOptions
            {
                TransferOptions = new StorageTransferOptions
                {
                    // Aumenta o tamanho do buffer e número máximo de threads
                    InitialTransferSize = 10 * 1024 * 1024,
                    MaximumConcurrency = 5
                }
            };
            
            using (var fileStream = file.OpenReadStream())
            {
                logger.LogInformation($"Uploading {fileName} of size {file.Length} bytes.");
                await blob.UploadAsync(fileStream, true);
                logger.LogInformation("Upload completed.");
            }
            
            logger.LogInformation($"File {fileName} uploaded successfully");
            
            return new OkObjectResult( new
            {
                Message = "File uploaded successfully",
                FileName = fileName,
                FileType = fileType,
                blobUri = blob.Uri
            });
        }
    }
}
