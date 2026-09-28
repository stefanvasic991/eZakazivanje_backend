using System;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using Microsoft.Extensions.Logging;
using eZakazivanje.Entity.Settings;
using Microsoft.Extensions.Options;
using eZakazivanje.DataService.Services;

namespace eZakazivanje.DataService.BackgroundServices.Services;

public class EmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        
        // Validate settings at startup
        _settings.Validate();
        
        _logger.LogInformation("Email service initialized");
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
        if (string.IsNullOrEmpty(to))
        {
            _logger.LogWarning("Pokušano slanje emaila na praznu email adresu");
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(new MailboxAddress("", to));
            message.Subject = subject;

            message.Body = new TextPart("plain")
            {
                Text = body
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.Username, _settings.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email");
            return false;
        }
    }

    public async Task<bool> SendBusinessApprovalRequestAsync(string phoneNumber, string businessName, string businessDescription, string businessEmail)
    {
        try
        {
            var adminEmail = "stefanvasic@softikos.com"; // Hardcoded admin email

            var (subject, emailBody) = NotificationMessages.AdminNewBusinessRegistrationEmail(
                phoneNumber,
                businessName,
                businessDescription,
                businessEmail);

            return await SendEmailAsync(adminEmail, subject, emailBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending business approval request");
            return false;
        }
    }

    public async Task<bool> SendBusinessApprovalLinkToUserAsync(
        string userEmail,
        string businessName,
        string approvalLink,
        string? preferredLanguage = null)
    {
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            _logger.LogWarning("Attempted to send business approval link to empty email");
            return false;
        }

        try
        {
            var (subject, emailBody) = NotificationMessages.BusinessActivationLinkEmail(
                preferredLanguage,
                businessName,
                approvalLink);

            return await SendEmailAsync(userEmail, subject, emailBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending business approval link to user");
            return false;
        }
    }
}
