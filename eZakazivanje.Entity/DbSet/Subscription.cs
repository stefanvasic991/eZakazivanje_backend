namespace eZakazivanje.Entity.DbSet;

public class Subscription : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Bussiness? Business { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    public SubscriptionPlan? SubscriptionPlan { get; set; }
    
    public string Platform { get; set; } = string.Empty; // "GooglePlay" or "Apple"
    public string PurchaseToken { get; set; } = string.Empty; // Google Play purchase token or Apple transaction ID
    public string OrderId { get; set; } = string.Empty; // Google Play order ID or Apple original transaction ID
    
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? AutoRenewDate { get; set; } // When subscription will auto-renew
    
    public bool IsActive { get; set; } = true;
    public bool IsAutoRenewing { get; set; } = false;
    public bool IsCancelled { get; set; } = false;
    public DateTime? CancelledDate { get; set; }
    
    public string? CancellationReason { get; set; }
    public string? VerificationStatus { get; set; } // "Verified", "Pending", "Failed"
    public DateTime? LastVerifiedAt { get; set; }
}

