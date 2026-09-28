using System;

namespace eZakazivanje.Entity.DbSet;

public class Address : BaseEntity
{
    public string? StreetName { get; set; }
    public string? Floor { get; set; }
    public string? ZipCode { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public double Longitude { get; set; }
    public double Latitude { get; set; }

    // Navigation properties
    public string? ApplicationUserId { get; set; }
    public virtual ApplicationUser? ApplicationUser { get; set; }

    public Guid? BusinessId { get; set; }
    public Bussiness? Businesses { get; set; }
}
