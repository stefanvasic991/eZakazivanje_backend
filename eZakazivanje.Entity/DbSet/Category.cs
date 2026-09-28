using System;

namespace eZakazivanje.Entity.DbSet;

public class Category : BaseEntity
{
    public string? Name { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }

    // Navigation properties
    public List<Bussiness>? Businesses { get; set; } = new List<Bussiness>();
    public List<Service>? Services { get; set; } = new List<Service>();
}
