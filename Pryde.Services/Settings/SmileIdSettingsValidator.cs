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

        var passengerValidation = ValidateOptions(
            settings.PassengerIdentityOptions,
            PassengerCombinations,
            nameof(settings.PassengerIdentityOptions));
        if (passengerValidation is not null)
        {
            return ValidateOptionsResult.Fail(passengerValidation);
        }

        var driverValidation = ValidateOptions(
            settings.DriverIdentityOptions,
            DriverCombinations,
            nameof(settings.DriverIdentityOptions));
        if (driverValidation is not null)
        {
            return ValidateOptionsResult.Fail(driverValidation);
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

    private static readonly HashSet<string> PassengerCombinations =
        new(StringComparer.Ordinal)
        {
            "NIN_SLIP|biometric_kyc",
            "VOTER_ID|biometric_kyc",
            "BVN|biometric_kyc",
            "PASSPORT|doc_verification"
        };

    private static readonly HashSet<string> DriverCombinations =
        new(StringComparer.Ordinal)
        {
            "DRIVERS_LICENSE|doc_verification"
        };

    private static string? ValidateOptions(
        IReadOnlyCollection<SmileIdIdentityOption>? options,
        HashSet<string> allowedCombinations,
        string propertyName)
    {
        if (options is null || !options.Any(option => option.Enabled))
        {
            return $"SmileId {propertyName} must contain at least one enabled option.";
        }

        var duplicates = options
            .GroupBy(
                option => $"{option.IdType?.Trim().ToUpperInvariant()}|{option.VerificationMethod?.Trim().ToLowerInvariant()}",
                StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicates is not null)
        {
            return $"SmileId {propertyName} contains duplicate option {duplicates.Key}.";
        }

        foreach (var option in options)
        {
            var idType = option.IdType?.Trim().ToUpperInvariant() ?? string.Empty;
            var method = option.VerificationMethod?.Trim().ToLowerInvariant() ?? string.Empty;
            var combination = $"{idType}|{method}";
            if (!allowedCombinations.Contains(combination))
            {
                return $"SmileId {propertyName} contains unsupported ID/product combination {combination}.";
            }

            option.IdType = idType;
            option.VerificationMethod = method;
        }

        return null;
    }
}
