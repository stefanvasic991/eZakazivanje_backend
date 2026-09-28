using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using eZakazivanje.DataService.Data;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;

namespace eZakazivanje.DataService.Repositories;

public class SubscriptionRepository : GenericRepository<Subscription>, ISubscriptionRepository
{
    public SubscriptionRepository(AppDbContext context, ILogger<SubscriptionRepository> logger) 
        : base(context, logger)
    {
    }

    public async Task<Subscription?> GetActiveSubscriptionByBusinessIdAsync(Guid businessId)
    {
        try
        {
            var subscription = await _dbSet
                .Include(s => s.SubscriptionPlan)
                .Include(s => s.Business)
                .Where(s => s.BusinessId == businessId 
                    && s.IsActive 
                    && !s.IsCancelled 
                    && s.EndDate > DateTime.UtcNow)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            _logger.LogInformation("Active subscription for business {BusinessId}: {Found}", 
                businessId, subscription != null);
            return subscription;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active subscription for business {BusinessId}", businessId);
            return null;
        }
    }

    public async Task<List<Subscription>> GetSubscriptionsByBusinessIdAsync(Guid businessId)
    {
        try
        {
            var subscriptions = await _dbSet
                .Include(s => s.SubscriptionPlan)
                .Where(s => s.BusinessId == businessId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            _logger.LogInformation("Found {Count} subscriptions for business {BusinessId}", 
                subscriptions.Count, businessId);
            return subscriptions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscriptions for business {BusinessId}", businessId);
            return new List<Subscription>();
        }
    }

    public async Task<Subscription?> GetSubscriptionByPurchaseTokenAsync(string purchaseToken, string platform)
    {
        try
        {
            var subscription = await _dbSet
                .Include(s => s.SubscriptionPlan)
                .Include(s => s.Business)
                .FirstOrDefaultAsync(s => s.PurchaseToken == purchaseToken && s.Platform == platform);

            _logger.LogInformation("Subscription by purchase token {Token}: {Found}", 
                purchaseToken, subscription != null);
            return subscription;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription by purchase token {Token}", purchaseToken);
            return null;
        }
    }

    public async Task<Subscription?> GetSubscriptionByOrderIdAsync(string orderId, string platform)
    {
        try
        {
            var subscription = await _dbSet
                .Include(s => s.SubscriptionPlan)
                .Include(s => s.Business)
                .FirstOrDefaultAsync(s => s.OrderId == orderId && s.Platform == platform);

            _logger.LogInformation("Subscription by order ID {OrderId}: {Found}", 
                orderId, subscription != null);
            return subscription;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription by order ID {OrderId}", orderId);
            return null;
        }
    }

    public async Task<SubscriptionPlan?> GetSubscriptionPlanByProductIdAsync(string productId, string platform)
    {
        try
        {
            var plan = await _context.Set<SubscriptionPlan>()
                .FirstOrDefaultAsync(p => p.ProductId == productId && p.Platform == platform && p.IsActive);

            _logger.LogInformation("Subscription plan by product ID {ProductId}: {Found}", 
                productId, plan != null);
            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription plan by product ID {ProductId}", productId);
            return null;
        }
    }

    public async Task<List<SubscriptionPlan>> GetAllActiveSubscriptionPlansAsync()
    {
        try
        {
            var plans = await _context.Set<SubscriptionPlan>()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Price)
                .ToListAsync();

            _logger.LogInformation("Found {Count} active subscription plans", plans.Count);
            return plans;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active subscription plans");
            return new List<SubscriptionPlan>();
        }
    }

    public async Task<SubscriptionPlan?> GetSubscriptionPlanByIdAsync(Guid planId)
    {
        try
        {
            var plan = await _context.Set<SubscriptionPlan>()
                .FindAsync(planId);

            _logger.LogInformation("Subscription plan by ID {PlanId}: {Found}", planId, plan != null);
            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription plan by ID {PlanId}", planId);
            return null;
        }
    }

    public async Task<bool> CancelSubscriptionAsync(Guid subscriptionId)
    {
        try
        {
            var subscription = await _dbSet.FindAsync(subscriptionId);
            if (subscription == null)
            {
                _logger.LogWarning("Subscription {SubscriptionId} not found for cancellation", subscriptionId);
                return false;
            }

            subscription.IsCancelled = true;
            subscription.IsActive = false;
            subscription.IsAutoRenewing = false;
            subscription.CancelledDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Subscription {SubscriptionId} cancelled successfully", subscriptionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling subscription {SubscriptionId}", subscriptionId);
            return false;
        }
    }

    public async Task<bool> UpdateSubscriptionStatusAsync(Guid subscriptionId, bool isActive, bool isAutoRenewing)
    {
        try
        {
            var subscription = await _dbSet.FindAsync(subscriptionId);
            if (subscription == null)
            {
                _logger.LogWarning("Subscription {SubscriptionId} not found for status update", subscriptionId);
                return false;
            }

            subscription.IsActive = isActive;
            subscription.IsAutoRenewing = isAutoRenewing;
            subscription.LastVerifiedAt = DateTime.UtcNow;
            subscription.VerificationStatus = "Verified";

            await _context.SaveChangesAsync();
            _logger.LogInformation("Subscription {SubscriptionId} status updated: Active={Active}, AutoRenew={AutoRenew}", 
                subscriptionId, isActive, isAutoRenewing);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating subscription status {SubscriptionId}", subscriptionId);
            return false;
        }
    }

    public async Task<List<Subscription>> GetSubscriptionsNeedingVerificationAsync(int hoursSinceLastVerification = 24)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-hoursSinceLastVerification);
            var subscriptions = await _dbSet
                .Include(s => s.SubscriptionPlan)
                .Include(s => s.Business)
                .Where(s => s.IsActive && !s.IsCancelled &&
                           (s.LastVerifiedAt == null || s.LastVerifiedAt < cutoffTime))
                .ToListAsync();

            _logger.LogInformation("Found {Count} subscriptions needing verification", subscriptions.Count);
            return subscriptions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscriptions needing verification");
            return new List<Subscription>();
        }
    }

    public async Task<List<Subscription>> GetSubscriptionsExpiringSoonAsync(int daysAhead = 7)
    {
        try
        {
            var expiryDate = DateTime.UtcNow.AddDays(daysAhead);
            var subscriptions = await _dbSet
                .Include(s => s.SubscriptionPlan)
                .Include(s => s.Business)
                .Where(s => s.IsActive && !s.IsCancelled &&
                           s.EndDate <= expiryDate && s.EndDate > DateTime.UtcNow)
                .ToListAsync();

            _logger.LogInformation("Found {Count} subscriptions expiring within {Days} days", subscriptions.Count, daysAhead);
            return subscriptions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscriptions expiring soon");
            return new List<Subscription>();
        }
    }
}

