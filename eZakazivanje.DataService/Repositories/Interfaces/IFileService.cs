using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace eZakazivanje.DataService.Repositories.Interfaces
{
    public interface IFileService
    {
    Task<string> UploadImageAsync(IFormFile file, string entityType, Guid entityId);
    Task<bool> DeleteImageAsync(string imagePath);
    bool IsValidImage(IFormFile file);
    Task<(byte[] FileContents, string ContentType)?> GetImageAsync(string imagePath);
    }
}