using System;

namespace eZakazivanje.Entity.DTOS.Request;

public class CreateAddress
{
    public string? StreetName { get; set; }
    public string? Floor { get; set; }
    public string? ZipCode { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
}
