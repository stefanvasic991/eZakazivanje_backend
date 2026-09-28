namespace eZakazivanje.Entity.DTOS.Request;

public class GooglePlayWebhookRequest
{
    public string? Message { get; set; }
    public GooglePlayNotification? SubscriptionNotification { get; set; }
    public GooglePlayTestNotification? TestNotification { get; set; }
}

public class GooglePlayNotification
{
    public string? Version { get; set; }
    public long? NotificationType { get; set; } // 1=SUBSCRIPTION_RECOVERED, 2=SUBSCRIPTION_RENEWED, 3=SUBSCRIPTION_CANCELED, etc.
    public string? PurchaseToken { get; set; }
    public string? SubscriptionId { get; set; }
}

public class GooglePlayTestNotification
{
    public string? Version { get; set; }
}

