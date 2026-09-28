using System;
using Microsoft.AspNetCore.Identity;

namespace eZakazivanje.Entity.DbSet;

public interface IBaseEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ApplicationUser : IdentityUser,IBaseEntity
{
     public string FirstName { get; set; } = string.Empty;
     public string LastName { get; set; } = string.Empty;
     public string? BusinessName { get; set; }
     public string? BusinessDescription { get; set; }
     public string? ImageUrl { get; set; } = string.Empty;
     public string? FCMToken { get; set; }
     /// <summary>BCP-47 style code (e.g. en, sr). Used for push notification text.</summary>
     public string PreferredLanguage { get; set; } = "en";
     public DateTime CreatedAt { get; set; }
     public DateTime UpdatedAt { get; set; }




     // Navigation property that will only be used for User role
     public virtual ICollection<Appointment>? Appointments { get; set; }
}
