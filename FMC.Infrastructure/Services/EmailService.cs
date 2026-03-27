using FMC.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace FMC.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of IEmailService using MailKit.
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

    public async Task SendEmailAsync(string toEmail, string subject, string body, IDictionary<string, byte[]>? attachments = null)
    {
        try
        {
            var emailMessage = new MimeMessage();
            var senderName = _config["SmtpSettings:SenderName"] ?? "FMC System";
            var senderEmail = _config["SmtpSettings:SenderEmail"] ?? "no-reply@fmc.com";
            
            emailMessage.From.Add(new MailboxAddress(senderName, senderEmail));
            emailMessage.To.Add(new MailboxAddress("", toEmail));
            emailMessage.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = body };

            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    // Add as LinkedResource with explicit Content-ID and MIME type
                    var resource = bodyBuilder.LinkedResources.Add(attachment.Key, attachment.Value);
                    resource.ContentId = attachment.Key;
                    resource.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
                }
            }

            emailMessage.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            var host = _config["SmtpSettings:Host"];
            var port = int.Parse(_config["SmtpSettings:Port"] ?? "587");
            var username = _config["SmtpSettings:Username"];
            var password = _config["SmtpSettings:Password"];

            await client.ConnectAsync(host ?? "smtp.gmail.com", port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username ?? "", password ?? "");
            await client.SendAsync(emailMessage);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Security Email sent to {ToEmail}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send security email to {ToEmail}", toEmail);
        }
    }
}
