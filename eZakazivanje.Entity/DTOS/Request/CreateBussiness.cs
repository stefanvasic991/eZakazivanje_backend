using System;
using Microsoft.AspNetCore.Http;

namespace eZakazivanje.Entity.DTOS.Request;

public class CreateBussiness
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public List<IFormFile>? ImageFiles { get; set; }
}
