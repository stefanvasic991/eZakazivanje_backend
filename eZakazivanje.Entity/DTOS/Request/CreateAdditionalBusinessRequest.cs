namespace eZakazivanje.Entity.DTOS.Request;

/// <summary>
/// Request DTO for creating an additional business (without image files)
/// </summary>
public class CreateAdditionalBusinessRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
}

