using Microsoft.Extensions.Options;

namespace Pryde.Services.Settings;

public class DojahSettingsValidator : IValidateOptions<DojahSettings>
{
    public ValidateOptionsResult Validate(string? name, DojahSettings settings)
    {
        if (!settings.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var missing = new List<string>();
        AddIfMissing(missing, settings.BaseUrl, nameof(settings.BaseUrl));
        AddIfMissing(missing, settings.AppId, nameof(settings.AppId));
        AddIfMissing(missing, settings.PublicKey, nameof(settings.PublicKey));
        AddIfMissing(missing, settings.PrivateKey, nameof(settings.PrivateKey));
        AddIfMissing(missing, settings.ShareableLink, nameof(settings.ShareableLink));

        if (missing.Count > 0)
        {
            return ValidateOptionsResult.Fail(
                $"Enabled Dojah configuration is missing: {string.Join(", ", missing)}.");
        }

        var untrimmed = new List<string>();
        AddIfUntrimmed(untrimmed, settings.BaseUrl, nameof(settings.BaseUrl));
        AddIfUntrimmed(untrimmed, settings.AppId, nameof(settings.AppId));
        AddIfUntrimmed(untrimmed, settings.PublicKey, nameof(settings.PublicKey));
        AddIfUntrimmed(untrimmed, settings.PrivateKey, nameof(settings.PrivateKey));
        AddIfUntrimmed(untrimmed, settings.ShareableLink, nameof(settings.ShareableLink));
        if (!string.IsNullOrEmpty(settings.ApiToken))
        {
            AddIfUntrimmed(untrimmed, settings.ApiToken, nameof(settings.ApiToken));
        }

        if (untrimmed.Count > 0)
        {
            return ValidateOptionsResult.Fail(
                $"Dojah configuration values must not contain leading or trailing whitespace: " +
                $"{string.Join(", ", untrimmed)}.");
        }

        if (settings.AppId.Equals(settings.PrivateKey, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                "Dojah AppId and PrivateKey must be different values.");
        }

        if (settings.PublicKey.Equals(settings.PrivateKey, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                "Dojah PublicKey and PrivateKey must be different values.");
        }

        if (!string.IsNullOrEmpty(settings.ApiToken) &&
            settings.AppId.Equals(settings.ApiToken, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                "Dojah AppId and ApiToken must be different values.");
        }

        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps)
        {
            return ValidateOptionsResult.Fail("Dojah BaseUrl must be an absolute HTTPS URL.");
        }

        if (!Uri.TryCreate(settings.ShareableLink, UriKind.Absolute, out var shareableUri) ||
            shareableUri.Scheme != Uri.UriSchemeHttps)
        {
            return ValidateOptionsResult.Fail("Dojah ShareableLink must be an absolute HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(GetWidgetId(shareableUri)))
        {
            return ValidateOptionsResult.Fail("Dojah ShareableLink must contain a widget_id query parameter.");
        }

        return ValidateOptionsResult.Success;
    }

    internal static string? GetWidgetId(Uri shareableUri)
    {
        foreach (var pair in shareableUri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (Uri.UnescapeDataString(parts[0]).Equals("widget_id", StringComparison.OrdinalIgnoreCase))
            {
                return parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : null;
            }
        }

        return null;
    }

    private static void AddIfMissing(List<string> missing, string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(name);
        }
    }

    private static void AddIfUntrimmed(
        List<string> untrimmed,
        string value,
        string name)
    {
        if (!value.Equals(value.Trim(), StringComparison.Ordinal))
        {
            untrimmed.Add(name);
        }
    }
}
