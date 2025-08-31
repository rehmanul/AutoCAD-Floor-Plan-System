using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FloorPlanAPI.Services
{
    public interface IFileStorageService
    {
        Task<string> UploadFileAsync(string fileName, Stream content);
        Task<Stream> DownloadFileAsync(string fileName);
        Task<string> GetSignedUrlAsync(string fileName, TimeSpan expiry, bool isWrite = false);
        Task DeleteFileAsync(string fileName);
    }

    public class AzureBlobStorageService : IFileStorageService
    {
        private readonly string _connectionString;
        private readonly string _containerName;

        public AzureBlobStorageService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("AzureStorage") ?? throw new InvalidOperationException("AzureStorage connection string not found");
            _containerName = config["Storage:ContainerName"] ?? throw new InvalidOperationException("Storage:ContainerName not found");
        }

        private BlobContainerClient GetContainerClient()
        {
            return new BlobContainerClient(_connectionString, _containerName);
        }

        public async Task<string> UploadFileAsync(string fileName, Stream content)
        {
            try
            {
                var containerClient = GetContainerClient();
                await containerClient.CreateIfNotExistsAsync();
                var blobClient = containerClient.GetBlobClient(fileName);
                await blobClient.UploadAsync(content, overwrite: true);
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to upload file to storage: {ex.Message}", ex);
            }
        }

        public async Task<Stream> DownloadFileAsync(string fileName)
        {
            var blobClient = GetContainerClient().GetBlobClient(fileName);
            var response = await blobClient.DownloadStreamingAsync();
            return response.Value.Content;
        }

        public Task<string> GetSignedUrlAsync(string fileName, TimeSpan expiry, bool isWrite = false)
        {
            var blobClient = GetContainerClient().GetBlobClient(fileName);
            
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _containerName,
                BlobName = fileName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.Add(expiry)
            };
            
            if(isWrite)
                sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);
            else
                sasBuilder.SetPermissions(BlobSasPermissions.Read);
                
            return Task.FromResult(blobClient.GenerateSasUri(sasBuilder).ToString());
        }

        public async Task DeleteFileAsync(string fileName)
        {
            var blobClient = GetContainerClient().GetBlobClient(fileName);
            await blobClient.DeleteIfExistsAsync();
        }
    }
}