namespace eZakazivanje.DataService.Services.Interfaces;

public interface IGooglePlayBillingService
{
    Task<(bool IsValid, string? OrderId, DateTime? PurchaseTime, DateTime? ExpiryTime, bool AutoRenewing, string? ErrorMessage)> 
        VerifyPurchaseAsync(string purchaseToken, string productId);

    Task<(bool Success, string? ErrorMessage)> CancelSubscriptionAsync(
        string purchaseToken,
        string cancellationType);
}

