namespace eZakazivanje.Entity.DTOS.Response;

public class CategoryWithServices
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public List<ServiceDetails> Services { get; set; } = new();
}

public class ServiceDetails
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    // ISO-4217 currency code, e.g. "RSD", "EUR", "USD"
    public string? Currency { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ImageUrl { get; set; }
}

public class BusinessDetailsResponse
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<string>? Images { get; set; }
    public TimeSpan? StartWorkHours { get; set; }
    public TimeSpan? EndWorkHours { get; set; }
    public bool IsActive { get; set; }
    public List<CategoryWithServices>? Categories { get; set; }
    public List<ServiceDetails>? Services { get; set; }
    public List<BusinessEmployeeDto>? Employees { get; set; }
    public string? PIB { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    public string? Ticker { get; set; }
} 