using eZakazivanje.Entity.DbSet;

namespace eZakazivanje.DataService.Repositories.Interfaces;

public interface ISubscriptionRepository : IGenericRepository<Subscription>
{
    Task<Subscription?> GetActiveSubscriptionByBusinessIdAsync(Guid businessId);
    Task<List<Subscription>> GetSubscriptionsByBusinessIdAsync(Guid businessId);
    Task<Subscription?> GetSubscriptionByPurchaseTokenAsync(string purchaseToken, string platform);
    Task<Subscription?> GetSubscriptionByOrderIdAsync(string orderId, string platform);
    Task<SubscriptionPlan?> GetSubscriptionPlanByProductIdAsync(string productId, string platform);
    Task<List<SubscriptionPlan>> GetAllActiveSubscriptionPlansAsync();
    Task<SubscriptionPlan?> GetSubscriptionPlanByIdAsync(Guid planId);
    Task<bool> CancelSubscriptionAsync(Guid subscriptionId);
    Task<bool> UpdateSubscriptionStatusAsync(Guid subscriptionId, bool isActive, bool isAutoRenewing);
    Task<List<Subscription>> GetSubscriptionsNeedingVerificationAsync(int hoursSinceLastVerification = 24);
    Task<List<Subscription>> GetSubscriptionsExpiringSoonAsync(int daysAhead = 7);
}

