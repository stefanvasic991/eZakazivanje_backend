namespace eZakazivanje.Entity.Settings;

public class AppleSettings
{
    public string KeyId { get; set; } = string.Empty;
    public string IssuerId { get; set; } = string.Empty;
    public string BundleId { get; set; } = string.Empty;
    public string PrivateKeyPath { get; set; } = string.Empty;
    /// <summary>True for sandbox (test purchases), false for production. Must match where the purchase was made.</summary>
    public bool UseSandbox { get; set; } = true;
}

