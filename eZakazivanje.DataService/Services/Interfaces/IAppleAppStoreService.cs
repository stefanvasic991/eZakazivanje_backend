namespace eZakazivanje.DataService.Services.Interfaces;

public interface IAppleAppStoreService
{
    Task<(bool IsValid, string? ErrorMessage, string? OriginalTransactionId, DateTime? PurchaseTime, DateTime? ExpiryTime, bool AutoRenewing)> 
        VerifyPurchaseAsync(string transactionId, string productId);
}

