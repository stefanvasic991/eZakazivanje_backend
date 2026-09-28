using System;

namespace eZakazivanje.Entity.DTOS.Responce;

public class ServiceUpdateDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    // ISO-4217 currency code, e.g. "RSD", "EUR", "USD"
    public string? Currency { get; set; }
    public TimeSpan Duration { get; set; }
}

