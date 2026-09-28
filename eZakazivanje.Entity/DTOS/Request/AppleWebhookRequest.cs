using System.Text.Json.Serialization;

namespace eZakazivanje.Entity.DTOS.Request;

public class AppleWebhookRequest
{
    [JsonPropertyName("signedPayload")]
    public string? SignedPayload { get; set; }
    
    // For decoded payload
    public AppleNotificationPayload? Payload { get; set; }
}

public class AppleNotificationPayload
{
    [JsonPropertyName("notificationType")]
    public string? NotificationType { get; set; } // SUBSCRIBED, DID_RENEW, DID_FAIL_TO_RENEW, etc.
    
    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }
    
    [JsonPropertyName("notificationUUID")]
    public string? NotificationUUID { get; set; }
    
    [JsonPropertyName("data")]
    public AppleNotificationData? Data { get; set; }
}

public class AppleNotificationData
{
    [JsonPropertyName("bundleId")]
    public string? BundleId { get; set; }
    
    [JsonPropertyName("environment")]
    public string? Environment { get; set; } // Sandbox or Production
    
    [JsonPropertyName("transactionInfo")]
    public AppleTransactionInfo? TransactionInfo { get; set; }
    
    [JsonPropertyName("renewalInfo")]
    public AppleRenewalInfo? RenewalInfo { get; set; }
}

public class AppleTransactionInfo
{
    [JsonPropertyName("transactionId")]
    public string? TransactionId { get; set; }
    
    [JsonPropertyName("originalTransactionId")]
    public string? OriginalTransactionId { get; set; }
    
    [JsonPropertyName("productId")]
    public string? ProductId { get; set; }
}

public class AppleRenewalInfo
{
    [JsonPropertyName("autoRenewStatus")]
    public int? AutoRenewStatus { get; set; }
}

