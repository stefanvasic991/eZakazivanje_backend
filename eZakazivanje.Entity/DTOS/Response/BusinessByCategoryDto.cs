using eZakazivanje.Entity.DbSet;

public class BusinessByCategoryDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public List<string>? ImageUrls { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
} 