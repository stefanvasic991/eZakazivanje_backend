using System;

namespace eZakazivanje.Entity.Settings;

public class EmailSettings
{
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrEmpty(SmtpServer))
            throw new InvalidOperationException("SMTP Server is not configured");
        if (SmtpPort <= 0)
            throw new InvalidOperationException("SMTP Port must be greater than 0");
        if (string.IsNullOrEmpty(Username))
            throw new InvalidOperationException("SMTP Username is not configured");
        if (string.IsNullOrEmpty(Password))
            throw new InvalidOperationException("SMTP Password is not configured");
        if (string.IsNullOrEmpty(SenderEmail))
            throw new InvalidOperationException("Sender Email is not configured");
        if (string.IsNullOrEmpty(SenderName))
            throw new InvalidOperationException("Sender Name is not configured");
    }
}
