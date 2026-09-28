public class ProfileResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new List<string>();
    public string ImageUrl { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? BusinessDescription { get; set; }
    /// <summary>Notification/UI language (e.g. en, sr).</summary>
    public string PreferredLanguage { get; set; } = "en";
} 