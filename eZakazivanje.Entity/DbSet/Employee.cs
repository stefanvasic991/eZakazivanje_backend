using System;

namespace eZakazivanje.Entity.DbSet;

public class Employee:BaseEntity
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ImageUrl { get; set; }
    public bool? IsAvailable { get; set; } = true;

    // Navigation properties
    public Guid? BusinessesId { get; set; }
    public Bussiness? Businesses { get; set; }

    public Guid? BusinessId { get; set; }
    public virtual Bussiness? Business { get; set; }

    //public Schedule Schedule { get; set; }

    public List<Appointment>? Appointments { get; set; } = new List<Appointment>();
    public List<Service>? Services { get; set; } = new List<Service>();
}
