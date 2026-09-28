using System;

namespace eZakazivanje.Entity.DTOS.Request;

public class CreateUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
