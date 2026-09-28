using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace eZakazivanje.DataService.Services;

public class BusinessApprovalLinkService
{
    private const string Purpose = "eZakazivanje.BusinessApproval.v1";
    private readonly IDataProtector _protector;

    public BusinessApprovalLinkService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string CreateToken(Guid businessId, string userId, DateTimeOffset expiresAtUtc)
    {
        var payload = $"{businessId:N}|{userId}|{expiresAtUtc.ToUnixTimeSeconds()}";
        var protectedPayload = _protector.Protect(payload);
        return Base64UrlEncode(Encoding.UTF8.GetBytes(protectedPayload));
    }

    public bool TryValidateToken(string token, out Guid businessId, out string userId)
    {
        businessId = Guid.Empty;
        userId = string.Empty;

        try
        {
            var protectedPayload = Encoding.UTF8.GetString(Base64UrlDecode(token));
            var payload = _protector.Unprotect(protectedPayload);

            var parts = payload.Split('|');
            if (parts.Length != 3) return false;

            if (!Guid.TryParseExact(parts[0], "N", out businessId)) return false;
            userId = parts[1];
            if (!long.TryParse(parts[2], out var exp)) return false;

            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp);
            if (DateTimeOffset.UtcNow > expiresAt) return false;

            return !string.IsNullOrWhiteSpace(userId);
        }
        catch
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}

