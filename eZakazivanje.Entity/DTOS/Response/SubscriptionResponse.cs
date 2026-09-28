namespace eZakazivanje.Entity.DTOS.Response;

public class SubscriptionResponse
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public Guid SubscriptionPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? AutoRenewDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsAutoRenewing { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime? CancelledDate { get; set; }
    public string? VerificationStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}

