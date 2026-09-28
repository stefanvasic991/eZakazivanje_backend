using System;

namespace eZakazivanje.Entity.DTOS.Request;

public class CreateEmployee
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
}
