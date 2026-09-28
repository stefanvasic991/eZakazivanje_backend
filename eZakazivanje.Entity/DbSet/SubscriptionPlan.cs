namespace eZakazivanje.Entity.DbSet;

public class SubscriptionPlan : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationDays { get; set; } // Duration in days (e.g., 30 for monthly, 365 for yearly)
    public string Platform { get; set; } = string.Empty; // "GooglePlay" or "Apple"
    public string ProductId { get; set; } = string.Empty; // Product ID from Google Play or Apple App Store
    public bool IsActive { get; set; } = true;
    public List<Subscription>? Subscriptions { get; set; } = new List<Subscription>();
}

