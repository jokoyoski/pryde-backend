using Microsoft.Extensions.Options;

namespace Pryde.Services.Settings;

public class PaystackSettingsValidator : IValidateOptions<PaystackSettings>
{
    public ValidateOptionsResult Validate(
        string? name,
        PaystackSettings settings)
    {
        if (!settings.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(settings.SecretKey))
        {
            return ValidateOptionsResult.Fail(
                "Enabled Paystack configuration is missing SecretKey.");
        }

        if (!Uri.TryCreate(
                settings.BaseUrl,
                UriKind.Absolute,
                out var baseUrl) ||
            baseUrl.Scheme != Uri.UriSchemeHttps)
        {
            return ValidateOptionsResult.Fail(
                "Paystack BaseUrl must be an absolute HTTPS URL.");
        }

        return ValidateOptionsResult.Success;
    }
}
