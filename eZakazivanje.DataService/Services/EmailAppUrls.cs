using Microsoft.Extensions.Configuration;

namespace eZakazivanje.DataService.Services;

/// <summary>
/// Builds links in emails. Default matches the original hardcoded production URL (no Sliplane env required).
/// Optional overrides: Frontend:BaseUrl, FRONTEND__BASE_URL, Backend:BaseUrl, BACKEND__BASE_URL.
/// AppUrl is not used here (it often points at a different API host and breaks links in email).
/// </summary>
public static class EmailAppUrls
{
    /// <summary>Original default from AuthService / AuthenticationController before configurable URLs.</summary>
    private const string DefaultPublicBaseUrl = "https://zakazi-me.sliplane.app";

    public static string GetEmailLinkBaseOrigin(IConfiguration configuration, string? requestFallback = null)
    {
        foreach (var candidate in new[]
        {
            configuration["Frontend:BaseUrl"],
            Environment.GetEnvironmentVariable("FRONTEND__BASE_URL"),
            configuration["FrontendUrl"],
            Environment.GetEnvironmentVariable("FrontendUrl"),
            configuration["Backend:BaseUrl"],
            Environment.GetEnvironmentVariable("BACKEND__BASE_URL"),
            requestFallback,
            DefaultPublicBaseUrl
        })
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate.TrimEnd('/');
        }

        return DefaultPublicBaseUrl;
    }

    public static string BuildVerifyEmailLink(IConfiguration configuration, string email, string token, string? requestBackendFallback = null)
    {
        var origin = GetEmailLinkBaseOrigin(configuration, requestBackendFallback);
        return $"{origin}/api/Authentication/verify-email-page?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
    }

    public static string BuildPasswordResetLink(IConfiguration configuration, string email, string token, string? requestBackendFallback = null)
    {
        var origin = GetEmailLinkBaseOrigin(configuration, requestBackendFallback);
        return $"{origin}/api/Authentication/reset-password-page?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
    }
}
