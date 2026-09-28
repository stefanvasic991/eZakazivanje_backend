using AutoMapper;
using Microsoft.Extensions.Logging;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.DataService.Services.Interfaces;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Request;
using eZakazivanje.Entity.DTOS.Response;
using Microsoft.EntityFrameworkCore;
using System;

namespace eZakazivanje.DataService.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGooglePlayBillingService _googlePlayService;
    private readonly IAppleAppStoreService _appleService;
    private readonly IMapper _mapper;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        IUnitOfWork unitOfWork,
        IGooglePlayBillingService googlePlayService,
        IAppleAppStoreService appleService,
        IMapper mapper,
        ILogger<SubscriptionService> logger)
    {
        _unitOfWork = unitOfWork;
        _googlePlayService = googlePlayService;
        _appleService = appleService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<(bool Success, string Message, List<SubscriptionResponse> Subscriptions)> 
        VerifyGooglePurchaseAsync(VerifyGooglePurchaseRequest request, string userId)
    {
        try
        {
            // Validate request
            if (request.BusinessIds == null || !request.BusinessIds.Any())
            {
                return (false, "At least one business ID is required", new List<SubscriptionResponse>());
            }

            // Get all businesses owned by user
            var userBusinessIds = await _unitOfWork.Context.Businesses
                .Where(b => b.UserId == userId)
                .Select(b => b.Id)
                .ToListAsync();

            // Verify user owns all requested businesses
            var invalidBusinessIds = request.BusinessIds.Except(userBusinessIds).ToList();
            if (invalidBusinessIds.Any())
            {
                return (false, $"User doesn't own the following businesses: {string.Join(", ", invalidBusinessIds)}", 
                    new List<SubscriptionResponse>());
            }

            // Get all businesses
            var businesses = new List<Bussiness>();
            foreach (var businessId in request.BusinessIds)
            {
                var business = await _unitOfWork.Bussinesses.GetById(businessId);
                if (business == null)
                {
                    return (false, $"Business {businessId} not found", new List<SubscriptionResponse>());
                }
                businesses.Add(business);
            }

            // Get subscription plan by product ID
            var plan = await _unitOfWork.Subscriptions.GetSubscriptionPlanByProductIdAsync(
                request.ProductId, "GooglePlay");
            if (plan == null)
            {
                return (false, "Subscription plan not found for this product", new List<SubscriptionResponse>());
            }

            // Verify purchase with Google Play (once for all businesses)
            var (isValid, orderId, purchaseTime, expiryTime, autoRenewing, errorMessage) = 
                await _googlePlayService.VerifyPurchaseAsync(request.PurchaseToken, request.ProductId);

            if (!isValid)
            {
                var message = !string.IsNullOrWhiteSpace(errorMessage) 
                    ? errorMessage 
                    : "Purchase verification failed. Please check the server logs for more details.";
                return (false, message, new List<SubscriptionResponse>());
            }

            // Create or update subscriptions for all businesses
            var createdSubscriptions = new List<SubscriptionResponse>();

            // Compute effective dates if Google doesn't return expiry (fall back to plan duration)
            var effectiveStartDate = purchaseTime ?? DateTime.UtcNow;
            var effectiveEndDate = expiryTime ?? effectiveStartDate.AddDays(plan.DurationDays);

            foreach (var business in businesses)
            {
                // Check if subscription already exists for this specific business with this purchase token
                var existingSubscription = await _unitOfWork.Context.Subscriptions
                    .FirstOrDefaultAsync(s => s.BusinessId == business.Id && 
                                            s.PurchaseToken == request.PurchaseToken && 
                                            s.Platform == "GooglePlay");

                Subscription subscription;

                if (existingSubscription != null)
                {
                    // Update existing subscription
                    var updatedEndDate = expiryTime ?? existingSubscription.EndDate;
                    existingSubscription.IsActive = updatedEndDate > DateTime.UtcNow;
                    existingSubscription.IsAutoRenewing = autoRenewing;
                    existingSubscription.EndDate = updatedEndDate;
                    existingSubscription.AutoRenewDate = autoRenewing ? updatedEndDate : null;
                    existingSubscription.LastVerifiedAt = DateTime.UtcNow;
                    existingSubscription.VerificationStatus = "Verified";
                    existingSubscription.OrderId = orderId ?? existingSubscription.OrderId;
                    subscription = existingSubscription;
                }
                else
                {
                    // Create new subscription
                    subscription = new Subscription
                    {
                        BusinessId = business.Id,
                        SubscriptionPlanId = plan.Id,
                        Platform = "GooglePlay",
                        PurchaseToken = request.PurchaseToken,
                        OrderId = orderId ?? request.OrderId ?? "",
                        StartDate = effectiveStartDate,
                        EndDate = effectiveEndDate,
                        AutoRenewDate = autoRenewing ? effectiveEndDate : null,
                        IsActive = effectiveEndDate > DateTime.UtcNow,
                        IsAutoRenewing = autoRenewing,
                        VerificationStatus = "Verified",
                        LastVerifiedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.Subscriptions.Add(subscription);
                }

                await _unitOfWork.CompleteAsync();

                var response = _mapper.Map<SubscriptionResponse>(subscription);
                response.BusinessName = business.Name ?? "";
                response.PlanName = plan.Name;
                createdSubscriptions.Add(response);

                _logger.LogInformation("Google Play subscription created/updated: BusinessId={BusinessId}, SubscriptionId={SubscriptionId}",
                    business.Id, subscription.Id);
            }

            return (true, $"Subscriptions created/updated successfully for {createdSubscriptions.Count} business(es)", createdSubscriptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Google Play purchase for businesses: {BusinessIds}", 
                string.Join(", ", request.BusinessIds));
            return (false, $"Error: {ex.Message}", new List<SubscriptionResponse>());
        }
    }

    public async Task<(bool Success, string Message, List<SubscriptionResponse> Subscriptions)> 
        VerifyApplePurchaseAsync(VerifyApplePurchaseRequest request, string userId)
        {
            try
            {
                // Validate request
                if (request.BusinessIds == null || !request.BusinessIds.Any())
                {
                    return (false, "At least one business ID is required", new List<SubscriptionResponse>());
                }

                // Get all businesses owned by user
                var userBusinessIds = await _unitOfWork.Context.Businesses
                    .Where(b => b.UserId == userId)
                    .Select(b => b.Id)
                    .ToListAsync();

                _logger.LogInformation("Apple verification - User {UserId} owns {Count} businesses: {BusinessIds}. Requested businesses: {RequestedBusinessIds}", 
                    userId, userBusinessIds.Count, string.Join(", ", userBusinessIds), string.Join(", ", request.BusinessIds));

                // Verify user owns all requested businesses
                var invalidBusinessIds = request.BusinessIds.Except(userBusinessIds).ToList();
                if (invalidBusinessIds.Any())
                {
                    _logger.LogWarning("Apple verification - User {UserId} doesn't own the following businesses: {InvalidBusinessIds}. User owns: {OwnedBusinessIds}", 
                        userId, string.Join(", ", invalidBusinessIds), string.Join(", ", userBusinessIds));
                    return (false, $"User doesn't own the following businesses: {string.Join(", ", invalidBusinessIds)}", 
                        new List<SubscriptionResponse>());
                }

                // Get all businesses
                var businesses = new List<Bussiness>();
                foreach (var businessId in request.BusinessIds)
                {
                    var business = await _unitOfWork.Bussinesses.GetById(businessId);
                    if (business == null)
                    {
                        _logger.LogWarning("Apple verification - Business {BusinessId} not found in database", businessId);
                        return (false, $"Business {businessId} not found", new List<SubscriptionResponse>());
                    }
                    
                    _logger.LogInformation("Apple verification - Found business: Id={BusinessId}, Name={BusinessName}, UserId={BusinessUserId}, IsActive={IsActive}", 
                        business.Id, business.Name, business.UserId, business.IsActive);
                    businesses.Add(business);
                }

            // Get subscription plan by product ID
            var plan = await _unitOfWork.Subscriptions.GetSubscriptionPlanByProductIdAsync(
                request.ProductId, "Apple");
            if (plan == null)
            {
                _logger.LogWarning("Subscription plan not found for ProductId: {ProductId}, Platform: Apple", request.ProductId);
                return (false, $"Subscription plan not found for product ID: {request.ProductId}", new List<SubscriptionResponse>());
            }

            // Verify purchase with Apple (once for all businesses)
            _logger.LogInformation("Verifying Apple purchase: TransactionId={TransactionId}, ProductId={ProductId}", 
                request.TransactionId, request.ProductId);
            var (isValid, errorMessage, originalTransactionId, purchaseTime, expiryTime, autoRenewing) = 
                await _appleService.VerifyPurchaseAsync(request.TransactionId, request.ProductId);

            if (!isValid)
            {
                var message = !string.IsNullOrWhiteSpace(errorMessage)
                    ? errorMessage
                    : "Purchase verification failed with Apple. Please check the transaction ID and product ID.";
                _logger.LogWarning("Apple purchase verification failed: {Message}. TransactionId={TransactionId}, ProductId={ProductId}", 
                    message, request.TransactionId, request.ProductId);
                return (false, message, new List<SubscriptionResponse>());
            }

            // Create or update subscriptions for all businesses
            var createdSubscriptions = new List<SubscriptionResponse>();

            foreach (var business in businesses)
            {
                // Check if subscription already exists for this specific business with this transaction ID
                var existingSubscription = await _unitOfWork.Context.Subscriptions
                    .FirstOrDefaultAsync(s => s.BusinessId == business.Id && 
                                            s.PurchaseToken == request.TransactionId && 
                                            s.Platform == "Apple");

                Subscription subscription;

                if (existingSubscription != null)
                {
                    // Update existing subscription
                    existingSubscription.IsActive = expiryTime.HasValue && expiryTime.Value > DateTime.UtcNow;
                    existingSubscription.IsAutoRenewing = autoRenewing;
                    existingSubscription.EndDate = expiryTime ?? existingSubscription.EndDate;
                    existingSubscription.AutoRenewDate = autoRenewing ? expiryTime : null;
                    existingSubscription.LastVerifiedAt = DateTime.UtcNow;
                    existingSubscription.VerificationStatus = "Verified";
                    existingSubscription.OrderId = originalTransactionId ?? existingSubscription.OrderId;
                    subscription = existingSubscription;
                }
                else
                {
                    // Create new subscription
                    subscription = new Subscription
                    {
                        BusinessId = business.Id,
                        SubscriptionPlanId = plan.Id,
                        Platform = "Apple",
                        PurchaseToken = request.TransactionId,
                        OrderId = originalTransactionId ?? request.OriginalTransactionId ?? request.TransactionId,
                        StartDate = purchaseTime ?? DateTime.UtcNow,
                        EndDate = expiryTime ?? DateTime.UtcNow.AddDays(plan.DurationDays),
                        AutoRenewDate = autoRenewing ? expiryTime : null,
                        IsActive = expiryTime.HasValue && expiryTime.Value > DateTime.UtcNow,
                        IsAutoRenewing = autoRenewing,
                        VerificationStatus = "Verified",
                        LastVerifiedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.Subscriptions.Add(subscription);
                }

                await _unitOfWork.CompleteAsync();

                var response = _mapper.Map<SubscriptionResponse>(subscription);
                response.BusinessName = business.Name ?? "";
                response.PlanName = plan.Name;
                createdSubscriptions.Add(response);

                _logger.LogInformation("Apple subscription created/updated: BusinessId={BusinessId}, SubscriptionId={SubscriptionId}",
                    business.Id, subscription.Id);
            }

            return (true, $"Subscriptions created/updated successfully for {createdSubscriptions.Count} business(es)", createdSubscriptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Apple purchase for businesses: {BusinessIds}", 
                string.Join(", ", request.BusinessIds));
            return (false, $"Error: {ex.Message}", new List<SubscriptionResponse>());
        }
    }

    public async Task<SubscriptionResponse?> GetActiveSubscriptionAsync(Guid businessId)
    {
        try
        {
            var subscription = await _unitOfWork.Subscriptions.GetActiveSubscriptionByBusinessIdAsync(businessId);
            if (subscription == null)
                return null;

            var response = _mapper.Map<SubscriptionResponse>(subscription);
            response.BusinessName = subscription.Business?.Name ?? "";
            response.PlanName = subscription.SubscriptionPlan?.Name ?? "";

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active subscription for business {BusinessId}", businessId);
            return null;
        }
    }

    public async Task<List<SubscriptionResponse>> GetBusinessSubscriptionsAsync(Guid businessId)
    {
        try
        {
            var subscriptions = await _unitOfWork.Subscriptions.GetSubscriptionsByBusinessIdAsync(businessId);
            return subscriptions.Select(s =>
            {
                var response = _mapper.Map<SubscriptionResponse>(s);
                response.BusinessName = s.Business?.Name ?? "";
                response.PlanName = s.SubscriptionPlan?.Name ?? "";
                return response;
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscriptions for business {BusinessId}", businessId);
            return new List<SubscriptionResponse>();
        }
    }

    public async Task<List<SubscriptionPlanResponse>> GetAvailablePlansAsync()
    {
        try
        {
            var plans = await _unitOfWork.Subscriptions.GetAllActiveSubscriptionPlansAsync();
            return _mapper.Map<List<SubscriptionPlanResponse>>(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available subscription plans");
            return new List<SubscriptionPlanResponse>();
        }
    }

    public async Task<bool> CancelSubscriptionAsync(Guid subscriptionId, Guid businessId)
    {
        try
        {
            var subscription = await _unitOfWork.Subscriptions.GetById(subscriptionId);
            if (subscription == null || subscription.BusinessId != businessId)
            {
                _logger.LogWarning("Subscription {SubscriptionId} not found or doesn't belong to business {BusinessId}",
                    subscriptionId, businessId);
                return false;
            }

            // Cancel in store first (best effort / platform dependent)
            if (string.Equals(subscription.Platform, "GooglePlay", StringComparison.OrdinalIgnoreCase))
            {
                var (storeSuccess, storeError) = await _googlePlayService.CancelSubscriptionAsync(
                    subscription.PurchaseToken,
                    "USER_REQUESTED_STOP_RENEWALS");

                if (!storeSuccess)
                {
                    var msg = $"Failed to cancel subscription in Google Play: {storeError ?? "Unknown error"}";
                    _logger.LogWarning("Google Play cancellation failed for SubscriptionId={SubscriptionId}: {Message}",
                        subscriptionId, msg);
                    throw new InvalidOperationException(msg);
                }
            }
            else if (string.Equals(subscription.Platform, "Apple", StringComparison.OrdinalIgnoreCase))
            {
                // Apple does not provide a server-side API to cancel / disable auto-renew on behalf of the user.
                // We can only cancel locally and instruct the user to cancel in App Store settings.
                _logger.LogInformation("Apple subscription cancellation requested for SubscriptionId={SubscriptionId}. Cancelling locally only.", subscriptionId);
            }

            var cancelled = await _unitOfWork.Subscriptions.CancelSubscriptionAsync(subscriptionId);
            return cancelled;
        }
        catch (InvalidOperationException)
        {
            // Store cancellation failed (e.g., Google Play). Bubble up so controller returns 500 with a useful message.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling subscription {SubscriptionId}", subscriptionId);
            return false;
        }
    }

    public async Task<bool> HandleGooglePlayWebhookAsync(GooglePlayWebhookRequest request)
    {
        try
        {
            // Handle test notifications
            if (request.TestNotification != null)
            {
                _logger.LogInformation("Received Google Play test notification");
                return true;
            }

            if (request.SubscriptionNotification == null || string.IsNullOrEmpty(request.SubscriptionNotification.PurchaseToken))
            {
                _logger.LogWarning("Invalid Google Play webhook request: missing subscription notification or purchase token");
                return false;
            }

            var notification = request.SubscriptionNotification;
            var notificationType = notification.NotificationType ?? 0;

            // Find subscription by purchase token
            var subscription = await _unitOfWork.Subscriptions
                .GetSubscriptionByPurchaseTokenAsync(notification.PurchaseToken, "GooglePlay");

            if (subscription == null)
            {
                _logger.LogWarning("Google Play webhook: Subscription not found for purchase token {Token}", notification.PurchaseToken);
                return false;
            }

            // Handle different notification types
            switch (notificationType)
            {
                case 1: // SUBSCRIPTION_RECOVERED
                    _logger.LogInformation("Google Play: Subscription recovered for {SubscriptionId}", subscription.Id);
                    subscription.IsActive = true;
                    subscription.IsCancelled = false;
                    subscription.VerificationStatus = "Recovered";
                    break;

                case 2: // SUBSCRIPTION_RENEWED
                    _logger.LogInformation("Google Play: Subscription renewed for {SubscriptionId}", subscription.Id);
                    // Re-verify to get new expiry date
                    if (subscription.SubscriptionPlan != null)
                    {
                        var (isValid, orderId, purchaseTime, expiryTime, autoRenewing, errorMessage) =
                            await _googlePlayService.VerifyPurchaseAsync(subscription.PurchaseToken, subscription.SubscriptionPlan.ProductId);
                        
                        if (!isValid && !string.IsNullOrWhiteSpace(errorMessage))
                        {
                            _logger.LogWarning("Google Play verification failed during renewal for subscription {SubscriptionId}: {Error}", 
                                subscription.Id, errorMessage);
                        }
                        
                        if (isValid && expiryTime.HasValue)
                        {
                            subscription.EndDate = expiryTime.Value;
                            subscription.IsActive = true;
                            subscription.IsAutoRenewing = autoRenewing;
                        }
                    }
                    subscription.VerificationStatus = "Renewed";
                    break;

                case 3: // SUBSCRIPTION_CANCELED
                    _logger.LogInformation("Google Play: Subscription cancelled for {SubscriptionId}", subscription.Id);
                    subscription.IsCancelled = true;
                    subscription.IsAutoRenewing = false;
                    subscription.CancelledDate = DateTime.UtcNow;
                    subscription.VerificationStatus = "Cancelled";
                    break;

                case 4: // SUBSCRIPTION_PURCHASED
                    _logger.LogInformation("Google Play: Subscription purchased for {SubscriptionId}", subscription.Id);
                    subscription.IsActive = true;
                    subscription.VerificationStatus = "Purchased";
                    break;

                case 5: // SUBSCRIPTION_ON_HOLD
                    _logger.LogInformation("Google Play: Subscription on hold for {SubscriptionId}", subscription.Id);
                    subscription.IsActive = false;
                    subscription.VerificationStatus = "OnHold";
                    break;

                case 6: // SUBSCRIPTION_IN_GRACE_PERIOD
                    _logger.LogInformation("Google Play: Subscription in grace period for {SubscriptionId}", subscription.Id);
                    subscription.IsActive = true; // Still active during grace period
                    subscription.VerificationStatus = "GracePeriod";
                    break;

                case 7: // SUBSCRIPTION_RESTARTED
                    _logger.LogInformation("Google Play: Subscription restarted for {SubscriptionId}", subscription.Id);
                    subscription.IsActive = true;
                    subscription.IsCancelled = false;
                    subscription.VerificationStatus = "Restarted";
                    break;

                case 8: // SUBSCRIPTION_PRICE_CHANGE_CONFIRMED
                    _logger.LogInformation("Google Play: Subscription price change confirmed for {SubscriptionId}", subscription.Id);
                    subscription.VerificationStatus = "PriceChangeConfirmed";
                    break;

                case 9: // SUBSCRIPTION_DEFERRED
                    _logger.LogInformation("Google Play: Subscription deferred for {SubscriptionId}", subscription.Id);
                    subscription.VerificationStatus = "Deferred";
                    break;

                case 10: // SUBSCRIPTION_PAUSED
                    _logger.LogInformation("Google Play: Subscription paused for {SubscriptionId}", subscription.Id);
                    subscription.IsActive = false;
                    subscription.VerificationStatus = "Paused";
                    break;

                case 11: // SUBSCRIPTION_PAUSE_SCHEDULE_CHANGED
                    _logger.LogInformation("Google Play: Subscription pause schedule changed for {SubscriptionId}", subscription.Id);
                    break;

                case 12: // SUBSCRIPTION_REVOKED
                    _logger.LogInformation("Google Play: Subscription revoked for {SubscriptionId}", subscription.Id);
                    subscription.IsActive = false;
                    subscription.IsCancelled = true;
                    subscription.CancelledDate = DateTime.UtcNow;
                    subscription.VerificationStatus = "Revoked";
                    break;

                case 13: // SUBSCRIPTION_EXPIRED
                    _logger.LogInformation("Google Play: Subscription expired for {SubscriptionId}", subscription.Id);
                    subscription.IsActive = false;
                    subscription.VerificationStatus = "Expired";
                    break;

                default:
                    _logger.LogWarning("Unknown Google Play notification type: {Type}", notificationType);
                    break;
            }

            subscription.LastVerifiedAt = DateTime.UtcNow;
            await _unitOfWork.CompleteAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Google Play webhook");
            return false;
        }
    }

    public async Task<bool> HandleAppleWebhookAsync(AppleWebhookRequest request)
    {
        try
        {
            // Validate request
            if (request == null)
            {
                _logger.LogWarning("Invalid Apple webhook request: request is null");
                return false;
            }

            // Note: In production, you should verify the signed payload from Apple
            // For now, we'll handle the decoded payload
            
            if (request.Payload?.Data?.TransactionInfo == null)
            {
                _logger.LogWarning("Invalid Apple webhook request: missing transaction info");
                return false;
            }

            var transactionInfo = request.Payload.Data.TransactionInfo;
            var notificationType = request.Payload.NotificationType ?? "";

            // Find subscription by transaction ID or original transaction ID
            var subscription = await _unitOfWork.Subscriptions
                .GetSubscriptionByOrderIdAsync(transactionInfo.OriginalTransactionId ?? transactionInfo.TransactionId ?? "", "Apple");

            if (subscription == null)
            {
                _logger.LogWarning("Apple webhook: Subscription not found for transaction {TransactionId}", 
                    transactionInfo.TransactionId);
                return false;
            }

            // Handle different notification types
            switch (notificationType.ToUpper())
            {
                case "SUBSCRIBED":
                    _logger.LogInformation("Apple: Subscription subscribed for {SubscriptionId}", subscription.Id);
                    subscription.IsActive = true;
                    subscription.VerificationStatus = "Subscribed";
                    break;

                case "DID_RENEW":
                    _logger.LogInformation("Apple: Subscription renewed for {SubscriptionId}", subscription.Id);
                    // Re-verify to get new expiry date
                    if (subscription.SubscriptionPlan != null && !string.IsNullOrEmpty(transactionInfo.TransactionId))
                    {
                        var (isValid, _, originalTransactionId, purchaseTime, expiryTime, autoRenewing) =
                            await _appleService.VerifyPurchaseAsync(transactionInfo.TransactionId, subscription.SubscriptionPlan.ProductId);
                        
                        if (isValid && expiryTime.HasValue)
                        {
                            subscription.EndDate = expiryTime.Value;
                            subscription.IsActive = true;
                            subscription.IsAutoRenewing = autoRenewing;
                        }
                    }
                    subscription.VerificationStatus = "Renewed";
                    break;

                case "DID_FAIL_TO_RENEW":
                    _logger.LogInformation("Apple: Subscription failed to renew for {SubscriptionId}", subscription.Id);
                    subscription.IsAutoRenewing = false;
                    subscription.VerificationStatus = "FailedToRenew";
                    break;

                case "DID_CHANGE_RENEWAL_PREF":
                    _logger.LogInformation("Apple: Subscription renewal preference changed for {SubscriptionId}", subscription.Id);
                    if (request.Payload.Data.RenewalInfo?.AutoRenewStatus == 1)
                    {
                        subscription.IsAutoRenewing = true;
                    }
                    else
                    {
                        subscription.IsAutoRenewing = false;
                    }
                    subscription.VerificationStatus = "RenewalPrefChanged";
                    break;

                case "DID_CHANGE_RENEWAL_STATUS":
                    _logger.LogInformation("Apple: Subscription renewal status changed for {SubscriptionId}", subscription.Id);
                    if (request.Payload.Data.RenewalInfo?.AutoRenewStatus == 1)
                    {
                        subscription.IsAutoRenewing = true;
                    }
                    else
                    {
                        subscription.IsAutoRenewing = false;
                    }
                    subscription.VerificationStatus = "RenewalStatusChanged";
                    break;

                case "EXPIRED":
                    _logger.LogInformation("Apple: Subscription expired for {SubscriptionId}", subscription.Id);
                    subscription.IsActive = false;
                    subscription.VerificationStatus = "Expired";
                    break;

                case "GRACE_PERIOD_EXPIRED":
                    _logger.LogInformation("Apple: Subscription grace period expired for {SubscriptionId}", subscription.Id);
                    subscription.IsActive = false;
                    subscription.VerificationStatus = "GracePeriodExpired";
                    break;

                case "REFUND":
                    _logger.LogInformation("Apple: Subscription refunded for {SubscriptionId}", subscription.Id);
                    subscription.IsActive = false;
                    subscription.IsCancelled = true;
                    subscription.CancelledDate = DateTime.UtcNow;
                    subscription.VerificationStatus = "Refunded";
                    break;

                case "REVOKE":
                    _logger.LogInformation("Apple: Subscription revoked for {SubscriptionId}", subscription.Id);
                    subscription.IsActive = false;
                    subscription.IsCancelled = true;
                    subscription.CancelledDate = DateTime.UtcNow;
                    subscription.VerificationStatus = "Revoked";
                    break;

                default:
                    _logger.LogWarning("Unknown Apple notification type: {Type}", notificationType);
                    break;
            }

            subscription.LastVerifiedAt = DateTime.UtcNow;
            await _unitOfWork.CompleteAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Apple webhook");
            return false;
        }
    }
}

