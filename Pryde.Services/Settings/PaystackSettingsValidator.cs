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

        if (string.IsNullOrWhiteSpace(settings.PublicKey))
        {
            return ValidateOptionsResult.Fail(
                "Enabled Paystack configuration is missing PublicKey.");
        }

        var expectedDomain = settings.ExpectedDomain.Trim().ToLowerInvariant();
        if (expectedDomain is not ("live" or "test"))
        {
            return ValidateOptionsResult.Fail(
                "Paystack ExpectedDomain must be live or test.");
        }

        var expectedPublicPrefix = expectedDomain == "live"
            ? "pk_live_"
            : "pk_test_";
        var expectedSecretPrefix = expectedDomain == "live"
            ? "sk_live_"
            : "sk_test_";
        if (!settings.PublicKey.StartsWith(
                expectedPublicPrefix,
                StringComparison.Ordinal) ||
            !settings.SecretKey.StartsWith(
                expectedSecretPrefix,
                StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                "Paystack public and secret keys must match ExpectedDomain.");
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
