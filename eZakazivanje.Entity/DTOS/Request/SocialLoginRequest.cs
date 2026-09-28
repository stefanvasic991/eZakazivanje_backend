using System.ComponentModel.DataAnnotations;

namespace eZakazivanje.Entity.DTOS.Request;

public class SocialLoginRequest
{
    [Required]
        public string Provider { get; set; } = string.Empty; // "Google", "Facebook", "Apple", "Microsoft"

    [Required]
    public string IdToken { get; set; } = string.Empty; // OAuth ID token from provider

    public string? AccessToken { get; set; } // Optional access token

    public string? Role { get; set; } // Optional: "User" or "Business" (defaults to "User")
}

