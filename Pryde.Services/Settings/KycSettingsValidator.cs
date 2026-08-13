using Microsoft.Extensions.Options;

namespace Pryde.Services.Settings;

public sealed class KycSettingsValidator : IValidateOptions<KycSettings>
{
    public ValidateOptionsResult Validate(string? name, KycSettings settings)
    {
        if (string.Equals(settings.ActiveProvider, "Dojah", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(settings.ActiveProvider, "SmileId", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            "Kyc ActiveProvider must be Dojah or SmileId.");
    }
}
