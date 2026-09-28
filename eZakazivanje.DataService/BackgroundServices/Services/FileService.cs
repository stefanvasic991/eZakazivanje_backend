using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using eZakazivanje.DataService.Repositories.Interfaces;

namespace eZakazivanje.DataService.BackgroundServices.Services
{
    public class FileService : IFileService
    {
        private readonly string _webRootPath;
        private readonly IConfiguration _configuration;
        private const int MaxFileSizeInMb = 1;
        private readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };

        public FileService(IConfiguration configuration)
        {
            _configuration = configuration;
            _webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            EnsureUploadDirectoryExists();
        }

        private void EnsureUploadDirectoryExists()
        {
            if (!Directory.Exists(_webRootPath))
            {
                Directory.CreateDirectory(_webRootPath);
            }

            var uploadsFolder = Path.Combine(_webRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }
        }

        public bool IsValidImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;

            // Check file size (1MB = 1 * 1024 * 1024 bytes)
            if (file.Length > MaxFileSizeInMb * 1024 * 1024)
                return false;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return AllowedExtensions.Contains(extension);
        }

        public async Task<string> UploadImageAsync(IFormFile file)
        {
            if (!IsValidImage(file))
                throw new ArgumentException("Neispravan format slike ili veličina slike");

            var uploadsFolder = Path.Combine(_webRootPath, "uploads");
            var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/uploads/{uniqueFileName}";
        }

        public async Task<(byte[] FileContents, string ContentType)?> GetImageAsync(string relativePath)
        {
            try
            {
                // Remove leading slash if present
                relativePath = relativePath.TrimStart('/');
                
                // Construct full path
                var fullPath = Path.Combine(_webRootPath, relativePath);
                
                // Verify the path is within wwwroot
                var normalizedRequestedPath = Path.GetFullPath(fullPath);
                var normalizedRootPath = Path.GetFullPath(_webRootPath);
                
                if (!normalizedRequestedPath.StartsWith(normalizedRootPath))
                {
                    return null;
                }

                if (!File.Exists(fullPath))
                {
                    return null;
                }

                var fileContents = await File.ReadAllBytesAsync(fullPath);
                var extension = Path.GetExtension(fullPath).ToLowerInvariant();
                
                var contentType = extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    _ => "application/octet-stream"
                };

                return (fileContents, contentType);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> DeleteImageAsync(string relativePath)
        {
            try
            {
                // Remove leading slash if present
                relativePath = relativePath.TrimStart('/');
                
                // Construct full path
                var fullPath = Path.Combine(_webRootPath, relativePath);
                
                // Verify the path is within wwwroot
                var normalizedRequestedPath = Path.GetFullPath(fullPath);
                var normalizedRootPath = Path.GetFullPath(_webRootPath);
                
                if (!normalizedRequestedPath.StartsWith(normalizedRootPath))
                {
                    return false;
                }

                if (!File.Exists(fullPath))
                {
                    return false;
                }

                await Task.Run(() => File.Delete(fullPath));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<string> UploadImageAsync(IFormFile file, string entityType, Guid entityId)
        {
            if (!IsValidImage(file))
                throw new ArgumentException("Neispravan format slike ili veličina slike");

            var entityFolder = Path.Combine(_webRootPath, "uploads", entityType.ToLower());
            if (!Directory.Exists(entityFolder))
            {
                Directory.CreateDirectory(entityFolder);
            }

            var uniqueFileName = $"{entityId}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(entityFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/uploads/{entityType.ToLower()}/{uniqueFileName}";
        }

        async Task<(byte[] FileContents, string ContentType)?> IFileService.GetImageAsync(string imagePath)
        {
            try
            {
                return await GetImageAsync(imagePath);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}