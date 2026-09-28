using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using eZakazivanje.DataService.Data;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.DataService.Services.Interfaces;
using eZakazivanje.Entity.DbSet;

namespace eZakazivanje.DataService.BackgroundServices;

public class SubscriptionVerificationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionVerificationService> _logger;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public SubscriptionVerificationService(
        IServiceScopeFactory scopeFactory,
        ILogger<SubscriptionVerificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SubscriptionVerificationService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Ensure only one instance runs at a time
                if (!await _semaphore.WaitAsync(0, stoppingToken))
                {
                    _logger.LogWarning("Previous subscription verification is still running, skipping this iteration");
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                    continue;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var googlePlayService = scope.ServiceProvider.GetRequiredService<IGooglePlayBillingService>();
                    var appleService = scope.ServiceProvider.GetRequiredService<IAppleAppStoreService>();

                    await VerifySubscriptionsAsync(unitOfWork, googlePlayService, appleService, stoppingToken);
                }
                finally
                {
                    _semaphore.Release();
                }

                // Run every 6 hours
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SubscriptionVerificationService");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    private async Task VerifySubscriptionsAsync(
        IUnitOfWork unitOfWork,
        IGooglePlayBillingService googlePlayService,
        IAppleAppStoreService appleService,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting subscription verification process");

            // Get subscriptions that need verification (haven't been verified in last 24 hours)
            var subscriptionsToVerify = await unitOfWork.Subscriptions.GetSubscriptionsNeedingVerificationAsync(24);

            if (!subscriptionsToVerify.Any())
            {
                _logger.LogInformation("No subscriptions need verification at this time");
                return;
            }

            _logger.LogInformation("Verifying {Count} subscriptions", subscriptionsToVerify.Count);

            int verifiedCount = 0;
            int failedCount = 0;

            foreach (var subscription in subscriptionsToVerify)
            {
                try
                {
                    bool isActive = false;
                    bool isAutoRenewing = false;
                    DateTime? expiryTime = null;

                    if (subscription.SubscriptionPlan == null)
                    {
                        _logger.LogWarning("Subscription {SubscriptionId} has no associated plan, skipping", subscription.Id);
                        continue;
                    }

                    string productId = subscription.SubscriptionPlan.ProductId;

                    if (string.IsNullOrEmpty(productId))
                    {
                        _logger.LogWarning("Subscription {SubscriptionId} has no product ID, skipping", subscription.Id);
                        continue;
                    }

                    if (subscription.Platform == "GooglePlay")
                    {
                        var (isValid, orderId, purchaseTime, expiry, autoRenewing, errorMessage) =
                            await googlePlayService.VerifyPurchaseAsync(subscription.PurchaseToken, productId);

                        if (!isValid && !string.IsNullOrWhiteSpace(errorMessage))
                        {
                            _logger.LogWarning("Google Play verification failed for subscription {SubscriptionId}: {Error}", 
                                subscription.Id, errorMessage);
                        }

                        isActive = isValid && expiry.HasValue && expiry.Value > DateTime.UtcNow;
                        isAutoRenewing = autoRenewing;
                        expiryTime = expiry;
                    }
                    else if (subscription.Platform == "Apple")
                    {
                        var (isValid, _, originalTransactionId, purchaseTime, expiry, autoRenewing) =
                            await appleService.VerifyPurchaseAsync(subscription.PurchaseToken, productId);

                        isActive = isValid && expiry.HasValue && expiry.Value > DateTime.UtcNow;
                        isAutoRenewing = autoRenewing;
                        expiryTime = expiry;
                    }

                    // Update subscription status
                    subscription.IsActive = isActive;
                    subscription.IsAutoRenewing = isAutoRenewing;
                    if (expiryTime.HasValue)
                    {
                        subscription.EndDate = expiryTime.Value;
                    }
                    subscription.LastVerifiedAt = DateTime.UtcNow;
                    subscription.VerificationStatus = isActive ? "Verified" : "Expired";

                    await unitOfWork.CompleteAsync();
                    verifiedCount++;

                    _logger.LogInformation("Verified subscription {SubscriptionId} for business {BusinessId}: Active={Active}",
                        subscription.Id, subscription.BusinessId, isActive);

                    // If subscription expired, optionally deactivate business
                    if (!isActive && subscription.Business != null)
                    {
                        _logger.LogWarning("Subscription {SubscriptionId} expired for business {BusinessId}",
                            subscription.Id, subscription.BusinessId);
                        // Optionally: subscription.Business.IsActive = false;
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogError(ex, "Error verifying subscription {SubscriptionId}", subscription.Id);
                    subscription.VerificationStatus = "Failed";
                    subscription.LastVerifiedAt = DateTime.UtcNow;
                    await unitOfWork.CompleteAsync();
                }
            }

            _logger.LogInformation("Subscription verification completed: {Verified} verified, {Failed} failed",
                verifiedCount, failedCount);

            // Check for subscriptions expiring soon and send notifications if needed
            await CheckExpiringSubscriptionsAsync(unitOfWork, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in subscription verification process");
        }
    }

    private async Task CheckExpiringSubscriptionsAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            // Get subscriptions expiring in the next 7 days
            var expiringSubscriptions = await unitOfWork.Subscriptions.GetSubscriptionsExpiringSoonAsync(7);

            foreach (var subscription in expiringSubscriptions)
            {
                var daysUntilExpiry = (subscription.EndDate - DateTime.UtcNow).Days;

                // Send notification if expiring in 3 days or less
                if (daysUntilExpiry <= 3 && subscription.Business != null)
                {
                    _logger.LogInformation("Subscription {SubscriptionId} for business {BusinessId} expires in {Days} days",
                        subscription.Id, subscription.BusinessId, daysUntilExpiry);

                    // TODO: Send notification to business owner about expiring subscription
                    // You can use NotificationService here if needed
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking expiring subscriptions");
        }
    }
}

