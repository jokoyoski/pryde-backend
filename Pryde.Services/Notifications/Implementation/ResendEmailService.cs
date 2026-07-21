using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Pryde.Services.Notifications.Interface;
using Pryde.Services.Settings;

namespace Pryde.Services.Notifications.Implementation;

public class ResendEmailService(
    HttpClient httpClient,
    IOptions<EmailSettings> emailSettings,
    ILogger<ResendEmailService> logger)
    : IEmailService
{
    private readonly EmailSettings _settings = emailSettings.Value;

    public async Task SendAsync(
        string toEmail, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
    {
        httpClient.BaseAddress ??= new Uri("https://api.resend.com/");
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);


        var payload = new
        {
            from = $"{_settings.FromName} <{_settings.FromAddress}>",
            to = new[] { toEmail },
            subject,
            html = htmlBody
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.PostAsync("emails", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Resend rejected an email request with status code {StatusCode}.",
                    (int)response.StatusCode);
            }

            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Resend email delivery failed.");
            throw;
        }
    }
}
