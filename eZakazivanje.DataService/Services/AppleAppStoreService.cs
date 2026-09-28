using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using eZakazivanje.Entity.Settings;
using eZakazivanje.DataService.Services.Interfaces;

namespace eZakazivanje.DataService.Services;

public class AppleAppStoreService : IAppleAppStoreService
{
    private readonly AppleSettings _settings;
    private readonly ILogger<AppleAppStoreService> _logger;
    private readonly HttpClient _httpClient;

    public AppleAppStoreService(
        IOptions<AppleSettings> settings,
        ILogger<AppleAppStoreService> logger,
        HttpClient httpClient)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClient = httpClient;
    }

    private string GenerateJWT()
    {
        try
        {
            var keyPath = Path.Combine(AppContext.BaseDirectory, _settings.PrivateKeyPath);

            if (!File.Exists(keyPath))
            {
                _logger.LogError("Apple private key file not found at: {Path}", keyPath);
                throw new FileNotFoundException($"Apple private key not found at: {keyPath}");
            }

            var privateKeyContent = File.ReadAllText(keyPath);
            var privateKey = privateKeyContent
                .Replace("-----BEGIN PRIVATE KEY-----", "")
                .Replace("-----END PRIVATE KEY-----", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Trim();

            var keyBytes = Convert.FromBase64String(privateKey);
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportPkcs8PrivateKey(keyBytes, out _);

            var now = DateTimeOffset.UtcNow;
            var payload = new Dictionary<string, object>
            {
                { "iss", _settings.IssuerId },
                { "iat", now.ToUnixTimeSeconds() },
                { "exp", now.AddMinutes(20).ToUnixTimeSeconds() },
                { "aud", "appstoreconnect-v1" },
                { "bid", _settings.BundleId }
            };

            var header = new Dictionary<string, object>
            {
                { "alg", "ES256" },
                { "kid", _settings.KeyId },
                { "typ", "JWT" }
            };

            // Create JWT manually (simplified - in production, use a proper JWT library)
            var headerJson = JsonSerializer.Serialize(header);
            var payloadJson = JsonSerializer.Serialize(payload);
            var headerBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(headerJson))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var message = $"{headerBase64}.{payloadBase64}";
            var signature = ecdsa.SignData(Encoding.UTF8.GetBytes(message), HashAlgorithmName.SHA256);
            var signatureBase64 = Convert.ToBase64String(signature)
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            return $"{message}.{signatureBase64}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Apple JWT");
            throw;
        }
    }

    public async Task<(bool IsValid, string? ErrorMessage, string? OriginalTransactionId, DateTime? PurchaseTime, DateTime? ExpiryTime, bool AutoRenewing)> 
        VerifyPurchaseAsync(string transactionId, string productId)
    {
        try
        {
            var jwt = GenerateJWT();
            var useSandboxFirst = _settings.UseSandbox;
            var productionUrl = "https://api.storekit.itunes.apple.com";
            var sandboxUrl = "https://api.storekit-sandbox.itunes.apple.com";

            // Try configured environment first
            var baseUrl = useSandboxFirst ? sandboxUrl : productionUrl;
            var (success, statusCode, content, parsedError) = await CallAppleSubscriptionApiAsync(jwt, baseUrl, transactionId);

            // If "Transaction id not found" (404), retry the other environment (e.g. TestFlight/sandbox purchase on production server)
            if (!success && (statusCode == 404 || IsTransactionNotFoundError(parsedError)))
            {
                var fallbackUrl = useSandboxFirst ? productionUrl : sandboxUrl;
                _logger.LogInformation("Apple: Transaction not found in {FirstEnv}, retrying {FallbackEnv}",
                    useSandboxFirst ? "sandbox" : "production", useSandboxFirst ? "production" : "sandbox");
                var (fallbackSuccess, _, fallbackContent, fallbackParsedError) = await CallAppleSubscriptionApiAsync(jwt, fallbackUrl, transactionId);
                if (fallbackSuccess)
                {
                    success = true;
                    content = fallbackContent;
                }
                else
                {
                    parsedError = fallbackParsedError ?? parsedError;
                }
            }

            if (!success)
            {
                var errorMessage = parsedError ?? $"Apple returned {(int)(statusCode ?? 0)}. Ensure TransactionId and ProductId are correct.";
                _logger.LogWarning("Apple purchase verification failed: Response={Response}, Message={Message}", content, errorMessage);
                return (false, errorMessage, null, null, null, false);
            }

            return ParseAppleSuccessResponse(content!, transactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Apple purchase: TransactionId={TransactionId}, ProductId={ProductId}",
                transactionId, productId);
            return (false, ex.Message, null, null, null, false);
        }
    }

    private static bool IsTransactionNotFoundError(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage)) return false;
        return errorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("Transaction id not found", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(bool Success, int? StatusCode, string? Content, string? ParsedError)> CallAppleSubscriptionApiAsync(
        string jwt, string baseUrl, string transactionId)
    {
        var url = $"{baseUrl}/inApps/v1/subscriptions/{transactionId}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        var parsedError = response.IsSuccessStatusCode ? null : TryParseAppleError(content);

        return (response.IsSuccessStatusCode, (int?)response.StatusCode, content, parsedError);
    }

    private (bool IsValid, string? ErrorMessage, string? OriginalTransactionId, DateTime? PurchaseTime, DateTime? ExpiryTime, bool AutoRenewing) ParseAppleSuccessResponse(string content, string transactionId)
    {
        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        JsonElement transactionData;

        if (root.TryGetProperty("signedTransactionInfo", out var signedTransactionInfo))
        {
            // Apple returns JWS: decode the payload (middle part) to get transaction JSON
            var payloadJson = DecodeJwsPayload(signedTransactionInfo.GetString());
            if (payloadJson == null)
            {
                _logger.LogWarning("Apple signedTransactionInfo could not be decoded");
                return (false, "Invalid Apple transaction payload.", null, null, null, false);
            }
            using var payloadDoc = JsonDocument.Parse(payloadJson);
            transactionData = payloadDoc.RootElement.Clone();
        }
        else if (root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array)
            {
                if (data.GetArrayLength() == 0)
                {
                    _logger.LogWarning("Apple response had empty data array");
                    return (false, "Apple returned no transaction data.", null, null, null, false);
                }
                transactionData = data[0];
            }
            else
            {
                transactionData = data;
            }
        }
        else
        {
            _logger.LogWarning("Apple response had neither 'data' nor 'signedTransactionInfo'. Keys: {Keys}",
                string.Join(", ", root.EnumerateObject().Select(p => p.Name)));
            return (false, "Apple response format not recognized (missing data or signedTransactionInfo).", null, null, null, false);
        }

        string? originalTransactionId = null;
        if (!TryGetString(transactionData, "originalTransactionId", out originalTransactionId) && !TryGetString(transactionData, "original_transaction_id", out originalTransactionId))
        {
            // First purchase or some responses: originalTransactionId equals transactionId; use payload or request id
            if (TryGetString(transactionData, "transactionId", out var payloadTransactionId) && !string.IsNullOrEmpty(payloadTransactionId))
                originalTransactionId = payloadTransactionId;
            else
                originalTransactionId = transactionId;
        }
        if (string.IsNullOrEmpty(originalTransactionId))
        {
            _logger.LogWarning("Apple payload keys: {Keys}", string.Join(", ", transactionData.EnumerateObject().Select(p => p.Name)));
            return (false, "Apple response missing originalTransactionId.", null, null, null, false);
        }
        long purchaseDateMs = 0;
        if (TryGetInt64(transactionData, "purchaseDate", out var pd)) purchaseDateMs = pd;
        else if (TryGetInt64(transactionData, "purchase_date", out pd)) purchaseDateMs = pd;
        else if (TryGetInt64(transactionData, "originalPurchaseDate", out pd)) purchaseDateMs = pd;
        else if (TryGetInt64(transactionData, "original_purchase_date", out pd)) purchaseDateMs = pd;
        else if (TryGetInt64(transactionData, "signedDate", out pd)) purchaseDateMs = pd;
        else if (TryGetInt64(transactionData, "signed_date", out pd)) purchaseDateMs = pd;

        var purchaseTime = purchaseDateMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(purchaseDateMs).DateTime
            : DateTime.UtcNow; // Fallback when payload has no date field; subscription can be updated later
        long? expiresDateMs = null;
        if (TryGetInt64(transactionData, "expiresDate", out var ed)) expiresDateMs = ed;
        else if (TryGetInt64(transactionData, "expires_date", out ed)) expiresDateMs = ed;
        else if (TryGetInt64(transactionData, "expirationDate", out ed)) expiresDateMs = ed;
        else if (TryGetInt64(transactionData, "expiration_date", out ed)) expiresDateMs = ed;

        var expiryTime = expiresDateMs.HasValue && expiresDateMs.Value > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(expiresDateMs.Value).DateTime
            : purchaseTime.AddYears(1); // Optional in some payloads; use 1 year from purchase so subscription is created
        var autoRenewStatus = TryGetInt32(transactionData, "autoRenewStatus", out var ar) && ar == 1
            || (TryGetInt32(transactionData, "auto_renew_status", out ar) && ar == 1);

        var isActive = expiryTime > DateTime.UtcNow;

        _logger.LogInformation(
            "Apple purchase verified: TransactionId={TransactionId}, Active={Active}, Expiry={Expiry}",
            originalTransactionId, isActive, expiryTime);

        return (isActive, null, originalTransactionId, purchaseTime, expiryTime, autoRenewStatus);
    }

    private static string? DecodeJwsPayload(string? jws)
    {
        if (string.IsNullOrEmpty(jws)) return null;
        var parts = jws.Split('.');
        if (parts.Length != 3) return null;
        var payloadBase64 = parts[1].Replace('-', '+').Replace('_', '/');
        switch (payloadBase64.Length % 4) { case 2: payloadBase64 += "=="; break; case 3: payloadBase64 += "="; break; }
        try
        {
            var bytes = Convert.FromBase64String(payloadBase64);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return null; }
    }

    private static bool TryGetString(JsonElement el, string name, out string? value)
    {
        value = null;
        if (!el.TryGetProperty(name, out var prop)) return false;
        value = prop.GetString();
        return true;
    }

    private static bool TryGetInt64(JsonElement el, string name, out long value)
    {
        value = 0;
        if (!el.TryGetProperty(name, out var prop)) return false;
        if (prop.TryGetInt64(out value)) return true;
        if (prop.ValueKind == JsonValueKind.String && long.TryParse(prop.GetString(), out value)) return true;
        return false;
    }

    private static bool TryGetInt32(JsonElement el, string name, out int value)
    {
        value = 0;
        if (!el.TryGetProperty(name, out var prop)) return false;
        if (prop.TryGetInt32(out value)) return true;
        if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out value)) return true;
        return false;
    }

    /// <summary>Try to extract a human-readable error from Apple's API error response JSON.</summary>
    private static string? TryParseAppleError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return null;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            // Apple may use errorMessage, message, or similar
            if (root.TryGetProperty("errorMessage", out var msg)) return msg.GetString();
            if (root.TryGetProperty("message", out var m)) return m.GetString();
            if (root.TryGetProperty("error", out var err))
            {
                if (err.ValueKind == JsonValueKind.String) return err.GetString();
                if (err.TryGetProperty("message", out var em)) return em.GetString();
            }
            return null;
        }
        catch { return null; }
    }
}

