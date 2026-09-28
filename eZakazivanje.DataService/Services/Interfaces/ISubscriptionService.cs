using eZakazivanje.Entity.DTOS.Request;
using eZakazivanje.Entity.DTOS.Response;

namespace eZakazivanje.DataService.Services.Interfaces;

public interface ISubscriptionService
{
    Task<(bool Success, string Message, List<SubscriptionResponse> Subscriptions)> 
        VerifyGooglePurchaseAsync(VerifyGooglePurchaseRequest request, string userId);
    Task<(bool Success, string Message, List<SubscriptionResponse> Subscriptions)> 
        VerifyApplePurchaseAsync(VerifyApplePurchaseRequest request, string userId);
    Task<SubscriptionResponse?> GetActiveSubscriptionAsync(Guid businessId);
    Task<List<SubscriptionResponse>> GetBusinessSubscriptionsAsync(Guid businessId);
    Task<List<SubscriptionPlanResponse>> GetAvailablePlansAsync();
    Task<bool> CancelSubscriptionAsync(Guid subscriptionId, Guid businessId);
    Task<bool> HandleGooglePlayWebhookAsync(GooglePlayWebhookRequest request);
    Task<bool> HandleAppleWebhookAsync(AppleWebhookRequest request);
}

