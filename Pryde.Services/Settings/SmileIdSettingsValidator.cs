using Microsoft.Extensions.Options;

namespace Pryde.Services.Settings;

public sealed class SmileIdSettingsValidator(IOptions<KycSettings> kycOptions)
    : IValidateOptions<SmileIdSettings>
{
    public ValidateOptionsResult Validate(string? name, SmileIdSettings settings)
    {
        if (!string.Equals(kycOptions.Value.ActiveProvider, "SmileId", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Success;
        }

        var missing = new List<string>();
        AddIfMissing(missing, settings.PartnerId, nameof(settings.PartnerId));
        AddIfMissing(missing, settings.ApiKey, nameof(settings.ApiKey));
        AddIfMissing(missing, settings.CallbackUrl, nameof(settings.CallbackUrl));
        AddIfMissing(missing, settings.RedirectUrl, nameof(settings.RedirectUrl));
        AddIfMissing(missing, settings.CompanyName, nameof(settings.CompanyName));
        AddIfMissing(missing, settings.DataPrivacyPolicyUrl, nameof(settings.DataPrivacyPolicyUrl));
        if (missing.Count > 0)
        {
            return ValidateOptionsResult.Fail(
                $"Selected SmileId configuration is missing: {string.Join(", ", missing)}.");
        }

        if (settings.Environment is not SmileIdSettings.Sandbox and not SmileIdSettings.Production)
        {
            return ValidateOptionsResult.Fail("SmileId Environment must be Sandbox or Production.");
        }

        if (!settings.IdentityType.Equals("NIN_V2", StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail("SmileId IdentityType must be the supported Nigerian value NIN_V2.");
        }

        if (settings.MaximumCallbackAgeMinutes <= 0)
        {
            return ValidateOptionsResult.Fail("SmileId MaximumCallbackAgeMinutes must be greater than zero.");
        }

        foreach (var value in new[]
                 {
                     settings.SandboxBaseUrl,
                     settings.ProductionBaseUrl,
                     settings.CallbackUrl,
                     settings.RedirectUrl,
                     settings.DataPrivacyPolicyUrl
                 })
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                return ValidateOptionsResult.Fail("SmileId URLs must be absolute HTTPS URLs.");
            }
        }

        return ValidateOptionsResult.Success;
    }

    private static void AddIfMissing(List<string> missing, string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(name);
        }
    }
}
