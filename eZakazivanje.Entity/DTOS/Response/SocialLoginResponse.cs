namespace eZakazivanje.Entity.DTOS.Response;

public class SocialLoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public List<string> Roles { get; set; } = new List<string>();
    public bool IsNewUser { get; set; }
    public string? BusinessId { get; set; }
    public string? BusinessIds { get; set; }
}

