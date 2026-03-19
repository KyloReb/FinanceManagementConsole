using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace FMC.Services;

/// <summary>
/// A MailKit-based implementation of the IEmailService.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Asynchronously connects to the configured SMTP server and sends an email.
    /// </summary>
    /// <param name="toEmail">The recipient's email address.</param>
    /// <param name="subject">The subject line of the email.</param>
    /// <param name="body">The full HTML or plaintext body of the email.</param>
    /// <param name="isHtml">True if the body contains HTML markup, false for plaintext. Defaults to true.</param>
    /// <returns>A Task representing the asynchronous connection and transmission operation.</returns>
    public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
    {
        try
        {
            var emailMessage = new MimeMessage();

            var senderName = _config["SmtpSettings:SenderName"];
            var senderEmail = _config["SmtpSettings:SenderEmail"];
            
            emailMessage.From.Add(new MailboxAddress(senderName, senderEmail));
            emailMessage.To.Add(new MailboxAddress("", toEmail));
            emailMessage.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = isHtml ? body : null,
                TextBody = isHtml ? null : body
            };

            emailMessage.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            var host = _config["SmtpSettings:Host"];
            var port = int.Parse(_config["SmtpSettings:Port"] ?? "587");
            var username = _config["SmtpSettings:Username"];
            var password = _config["SmtpSettings:Password"];

            // Connect using SecureSocketOptions.StartTls for standard SMTP
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);

            await client.SendAsync(emailMessage);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully to {ToEmail} with subject '{Subject}'", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail}. If this is a development environment, verify SMTP settings.", toEmail);
            // throw; // Rethrow to let the caller handle the failure -- DISABLED for Dev testing bypassing MailKit credentials
        }
    }
}
