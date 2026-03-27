namespace FMC.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, IDictionary<string, byte[]>? attachments = null);
}
