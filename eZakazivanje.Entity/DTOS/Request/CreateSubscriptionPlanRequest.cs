using System.ComponentModel.DataAnnotations;

namespace eZakazivanje.Entity.DTOS.Request;

public class CreateSubscriptionPlanRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [Range(0.01, 999999.99)]
    public decimal Price { get; set; }

    [Required]
    [Range(1, 3650)] // 1 day to 10 years
    public int DurationDays { get; set; }

    [Required]
    [RegularExpression("^(GooglePlay|Apple)$", ErrorMessage = "Platform must be either 'GooglePlay' or 'Apple'")]
    public string Platform { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string ProductId { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

