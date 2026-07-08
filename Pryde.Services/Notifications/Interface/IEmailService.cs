namespace Pryde.Services.Notifications.Interface;
public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}