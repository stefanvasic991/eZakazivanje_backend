namespace eZakazivanje.Entity.DTOS.Response;

public class BusinessEmployeeDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; }
} 