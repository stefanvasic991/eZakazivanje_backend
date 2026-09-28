using Microsoft.AspNetCore.Http;
using System;

public class ImageUploadRequest
{
    public IFormFile ImageFile { get; set; } = null!;
    public string EntityType { get; set; } = null!; // "user", "category", "service", "employee"
    public Guid EntityId { get; set; }
} 