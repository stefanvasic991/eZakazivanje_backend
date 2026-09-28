using System;

namespace eZakazivanje.Entity.DTOS.Request;

public class CreateCategory
{
    public string? Name { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
}
