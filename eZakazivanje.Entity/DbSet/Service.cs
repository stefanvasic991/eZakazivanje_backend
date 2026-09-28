using System;

namespace eZakazivanje.Entity.DbSet;

public class Service : BaseEntity
{
    public string? Name { get; set; }
    public TimeSpan? Duration { get; set; }
    public decimal? Price { get; set; }
    // ISO-4217 currency code, e.g. "RSD", "EUR", "USD"
    public string Currency { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }

    // Navigation properties
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    public List<Employee>? Employees { get; set; } = new List<Employee>();

    public Guid? BusinessId { get; set; }
    public Bussiness? Business { get; set; }

    public List<Appointment>? Appointments { get; set; } = new List<Appointment>();
}
