using System;
using System.IO;
using System.Threading.Tasks;

namespace FloorPlanAPI.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly string _baseUrl;
        private readonly string _storagePath;

        public LocalFileStorageService(IConfiguration configuration)
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

        public async Task<string> GetSignedUrlAsync(string fileName, TimeSpan expiry, bool forWrite = false)
        {
            return $"{_baseUrl}/files/{fileName}";
        }

        public async Task<Stream> DownloadFileAsync(string fileName)
        {
            var filePath = Path.Combine(_storagePath, fileName);
            return new FileStream(filePath, FileMode.Open, FileAccess.Read);
        }
    }
}