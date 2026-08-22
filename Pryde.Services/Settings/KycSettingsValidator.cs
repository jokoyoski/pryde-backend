using Microsoft.Extensions.Options;

namespace Pryde.Services.Settings;

public sealed class KycSettingsValidator : IValidateOptions<KycSettings>
{
    public ValidateOptionsResult Validate(string? name, KycSettings settings)
    {
        if (settings.MaxAttemptsPerMonth < 1)
        {
            return ValidateOptionsResult.Fail(
                "Kyc MaxAttemptsPerMonth must be at least 1.");
        }

        return string.Equals(settings.ActiveProvider, "Dojah", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(settings.ActiveProvider, "SmileId", StringComparison.OrdinalIgnoreCase)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "Kyc ActiveProvider must be Dojah or SmileId.");
    }
}
