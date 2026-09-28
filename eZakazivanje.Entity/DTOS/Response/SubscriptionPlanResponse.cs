namespace eZakazivanje.Entity.DTOS.Response;

public class SubscriptionPlanResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

