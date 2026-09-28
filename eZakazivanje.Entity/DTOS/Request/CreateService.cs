using System;

namespace eZakazivanje.Entity.DTOS.Request;

public class CreateService
{
    public string? Name { get; set; }
    public string? Duration { get; set; }
    public decimal Price { get; set; }
    // ISO-4217 currency code, e.g. "RSD", "EUR", "USD"
    public string? Currency { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }

}
