namespace FMC.Services;

/// <summary>
/// Defines the contract for an email sending service.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email asynchronously.
    /// </summary>
    /// <param name="toEmail">The recipient's email address.</param>
    /// <param name="subject">The subject line of the email.</param>
    /// <param name="body">The email content (supports HTML).</param>
    /// <param name="isHtml">Indicates if the body content should be parsed as HTML.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true);
}
