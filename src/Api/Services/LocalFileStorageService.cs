using System;
using System.IO;
using System.Threading.Tasks;

namespace FloorPlanAPI.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly string _baseUrl;
        private readonly string _storagePath;

        public LocalFileStorageService()
        {
            _baseUrl = "https://autocad-floor-plan-system.onrender.com";
            _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "storage");
            Directory.CreateDirectory(_storagePath);
        }

        public async Task<string> UploadFileAsync(string fileName, Stream content)
        {
            var filePath = Path.Combine(_storagePath, fileName);
            
            using var fileStream = new FileStream(filePath, FileMode.Create);
            await content.CopyToAsync(fileStream);
            
            return $"{_baseUrl}/files/{fileName}";
        }

        public Task<string> GetSignedUrlAsync(string fileName, TimeSpan expiry, bool forWrite = false)
        {
            return Task.FromResult($"{_baseUrl}/files/{fileName}");
        }

        public Task<Stream> DownloadFileAsync(string fileName)
        {
            var filePath = Path.Combine(_storagePath, fileName);
            return Task.FromResult<Stream>(new FileStream(filePath, FileMode.Open, FileAccess.Read));
        }

        public Task DeleteFileAsync(string fileName)
        {
            var filePath = Path.Combine(_storagePath, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return Task.CompletedTask;
        }
    }
}