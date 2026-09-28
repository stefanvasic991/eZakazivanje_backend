using System;

namespace eZakazivanje.Entity.DTOS.Response;

public class BusinessUserDetailsDto
{
    public string UserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public bool PhoneNumberConfirmed { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessDescription { get; set; } = string.Empty;
    public string FCMToken { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> Roles { get; set; } = new List<string>();
    public BusinessInfoDto? BusinessInfo { get; set; }
}

public class BusinessInfoDto
{
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    public TimeSpan StartWorkHours { get; set; }
    public TimeSpan EndWorkHours { get; set; }
    public TimeSpan? Tick { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public AddressDto? Address { get; set; }
    public int ServicesCount { get; set; }
    public int EmployeesCount { get; set; }
    public List<string> Images { get; set; } = new List<string>();
    public string PIB { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
