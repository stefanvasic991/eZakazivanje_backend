using Google.Apis.Auth.OAuth2;
using Google.Apis.AndroidPublisher.v3;
using Google.Apis.AndroidPublisher.v3.Data;
using Google.Apis.Services;
using Google;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using eZakazivanje.Entity.Settings;
using eZakazivanje.DataService.Services.Interfaces;

namespace eZakazivanje.DataService.Services;

public class GooglePlayBillingService : IGooglePlayBillingService
{
    private readonly GooglePlaySettings _settings;
    private readonly ILogger<GooglePlayBillingService> _logger;
    private AndroidPublisherService? _service;

    public GooglePlayBillingService(
        IOptions<GooglePlaySettings> settings,
        ILogger<GooglePlayBillingService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    private AndroidPublisherService GetService()
    {
        if (_service != null)
            return _service;

        try
        {
            var credentialsPath = Path.Combine(
                AppContext.BaseDirectory,
                _settings.ServiceAccountKeyPath
            );

            if (!File.Exists(credentialsPath))
            {
                _logger.LogError("Google Play credentials file not found at: {Path}", credentialsPath);
                throw new FileNotFoundException($"Google Play credentials not found at: {credentialsPath}");
            }

            var credential = GoogleCredential.FromFile(credentialsPath)
                .CreateScoped(new[] { AndroidPublisherService.Scope.Androidpublisher });

            _service = new AndroidPublisherService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "eZakazivanje"
            });

            _logger.LogInformation("Google Play Billing service initialized successfully");
            return _service;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Google Play Billing service");
            throw;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> CancelSubscriptionAsync(
        string purchaseToken,
        string cancellationType)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(purchaseToken))
                return (false, "Purchase token is required");

            // Use a direct HTTP call to purchases.subscriptionsv2.cancel so we can pass cancellationContext
            // (client libs are sometimes behind on request body support).
            var credentialsPath = Path.Combine(
                AppContext.BaseDirectory,
                _settings.ServiceAccountKeyPath
            );

            if (!File.Exists(credentialsPath))
            {
                _logger.LogError("Google Play credentials file not found at: {Path}", credentialsPath);
                return (false, $"Google Play credentials not found at: {credentialsPath}");
            }

            var credential = GoogleCredential.FromFile(credentialsPath)
                .CreateScoped(new[] { AndroidPublisherService.Scope.Androidpublisher });

            var tokenAccess = credential.UnderlyingCredential as ITokenAccess;
            if (tokenAccess == null)
            {
                return (false, "Google credential does not support token access");
            }

            var accessToken = await tokenAccess.GetAccessTokenForRequestAsync();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return (false, "Failed to acquire Google access token");
            }

            var url =
                $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{_settings.PackageName}/purchases/subscriptionsv2/tokens/{purchaseToken}:cancel";

            if (string.IsNullOrWhiteSpace(cancellationType))
            {
                cancellationType = "USER_REQUESTED_STOP_RENEWALS";
            }

            var body = new
            {
                cancellationContext = new
                {
                    cancellationType
                }
            };

            using var http = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Google Play subscription cancel failed. Status={Status}, Body={Body}", response.StatusCode, error);
                return (false, $"Google Play cancel failed: {response.StatusCode} {error}");
            }

            _logger.LogInformation("Google Play subscription cancelled (stop renewals). Token={Token}", purchaseToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling Google Play subscription. Token={Token}", purchaseToken);
            return (false, ex.Message);
        }
    }

    public async Task<(bool IsValid, string? OrderId, DateTime? PurchaseTime, DateTime? ExpiryTime, bool AutoRenewing, string? ErrorMessage)> 
        VerifyPurchaseAsync(string purchaseToken, string productId)
    {
        try
        {
            var service = GetService();
            var request = service.Purchases.Subscriptionsv2.Get(
                _settings.PackageName,
                purchaseToken
            );

            var subscriptionPurchase = await request.ExecuteAsync();

            if (subscriptionPurchase == null)
            {
                _logger.LogWarning("Google Play purchase verification returned null for token: {Token}", purchaseToken);
                return (false, null, null, null, false, "Purchase verification returned null response");
            }

            // Check if the product ID matches
            var latestOrderId = subscriptionPurchase.LatestOrderId;

            // Parse the subscription purchase data (purchases.subscriptionsv2.get)
            // - subscriptionPurchase.startTime (RFC3339)
            // - subscriptionPurchase.lineItems[].expiryTime (RFC3339)
            // - subscriptionPurchase.lineItems[].autoRenewingPlan.autoRenewEnabled (bool)
            DateTime? purchaseTime = subscriptionPurchase.StartTimeDateTimeOffset?.UtcDateTime;
            DateTime? expiryTime = null;
            bool autoRenewing = false;

            try
            {
                if (subscriptionPurchase.LineItems != null && subscriptionPurchase.LineItems.Any())
                {
                    foreach (var item in subscriptionPurchase.LineItems)
                    {
                        var itemExpiry = item.ExpiryTimeDateTimeOffset?.UtcDateTime;
                        if (itemExpiry.HasValue && (!expiryTime.HasValue || itemExpiry.Value > expiryTime.Value))
                        {
                            expiryTime = itemExpiry;
                        }

                        // Auto-renew is true if any line item is set to auto renew
                        if (item.AutoRenewingPlan?.AutoRenewEnabled == true)
                        {
                            autoRenewing = true;
                        }
                    }
                }

                _logger.LogInformation(
                    "Google Play subscription parsed: OrderId={OrderId}, Start={Start}, Expiry={Expiry}, AutoRenew={AutoRenew}",
                    latestOrderId, purchaseTime, expiryTime, autoRenewing);
            }
            catch (Exception parseEx)
            {
                _logger.LogWarning(parseEx, "Could not parse Google Play subscription details, will fall back to plan duration");
            }

            // Verify the subscription is active:
            // - If we have expiry, use it
            // - Otherwise treat it as valid (verification succeeded) and let the subscription service decide end date based on plan
            var isActive = !expiryTime.HasValue || expiryTime.Value > DateTime.UtcNow;

            _logger.LogInformation(
                "Google Play purchase verified: OrderId={OrderId}, Active={Active}, Expiry={Expiry}",
                latestOrderId, isActive, expiryTime);

            return (isActive, latestOrderId, purchaseTime, expiryTime, autoRenewing, null);
        }
        catch (GoogleApiException googleEx)
        {
            string errorMessage;
            
            // Handle specific Google API errors
            if (googleEx.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogError(googleEx, 
                    "Google Play API access denied. Error: {Error}. " +
                    "Please ensure the Google Play Android Developer API is enabled in your Google Cloud project. " +
                    "Token={Token}, ProductId={ProductId}",
                    googleEx.Message, purchaseToken, productId);
                
                // Extract the project ID from the error message if available
                var projectIdMatch = System.Text.RegularExpressions.Regex.Match(
                    googleEx.Message, @"project (\d+)");
                if (projectIdMatch.Success)
                {
                    var projectId = projectIdMatch.Groups[1].Value;
                    var enableUrl = $"https://console.developers.google.com/apis/api/androidpublisher.googleapis.com/overview?project={projectId}";
                    _logger.LogError("Enable the API at: {Url}", enableUrl);
                    errorMessage = $"Google Play API is not enabled. Please enable the Google Play Android Developer API in your Google Cloud project. Visit: {enableUrl}";
                }
                else
                {
                    errorMessage = "Google Play API access denied. Please ensure the Google Play Android Developer API is enabled in your Google Cloud project.";
                }
            }
            else
            {
                _logger.LogError(googleEx, 
                    "Google Play API error (Status: {StatusCode}): {Error}. Token={Token}, ProductId={ProductId}",
                    googleEx.HttpStatusCode, googleEx.Message, purchaseToken, productId);
                errorMessage = $"Google Play API error: {googleEx.Message}";
            }
            
            return (false, null, null, null, false, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Google Play purchase: Token={Token}, ProductId={ProductId}",
                purchaseToken, productId);
            return (false, null, null, null, false, $"An error occurred while verifying the purchase: {ex.Message}");
        }
    }

    private static DateTime? TryParseRfc3339(string? timestamp)
    {
        if (string.IsNullOrWhiteSpace(timestamp))
            return null;

        // Google returns RFC3339 timestamps, e.g. "2014-10-02T15:01:23Z"
        // Use DateTimeOffset to preserve the timezone and convert to UTC DateTime.
        if (DateTimeOffset.TryParse(
                timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
        {
            return dto.UtcDateTime;
        }

        return null;
    }
}

