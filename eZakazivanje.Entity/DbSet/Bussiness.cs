using System;
using System.Text.Json.Serialization;

namespace eZakazivanje.Entity.DbSet;

public class Bussiness : BaseEntity
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<Address>? Addresses { get; set; } = new List<Address>();
    [JsonIgnore]
    public List<Employee>? Employees { get; set; } = new List<Employee>();
    public List<Category>? Categories { get; set; } = new List<Category>();
    public TimeSpan? StartWorkHours { get; set; }
    public TimeSpan? EndWorkHours { get; set; }
    public List<string>? ImageUrls { get; set; } = new List<string>();
    public List<Appointment>? Appointments { get; set; } = new List<Appointment>();
    public List<Comment>? Comments { get; set; } = new List<Comment>();
    public bool? IsActive { get; set; } = true;
    public List<Service>? Services { get; set; } = new List<Service>();
    public string? PIB { get; set; }
    public string? PhoneNumber { get; set; }
    public TimeSpan? Tick {get; set;}
    public BusinessApprovalStatus ApprovalStatus { get; set; } = BusinessApprovalStatus.Pending;
    
    // Owner/User who created this business (allows multiple businesses per user)
    public string UserId { get; set; } = string.Empty;
    
    [JsonIgnore]
    public List<Subscription>? Subscriptions { get; set; } = new List<Subscription>();
}
