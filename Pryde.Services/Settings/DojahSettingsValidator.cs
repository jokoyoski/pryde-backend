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
        AddIfMissing(missing, settings.AppId, nameof(settings.AppId));
        AddIfMissing(missing, settings.PublicKey, nameof(settings.PublicKey));
        AddIfMissing(missing, settings.PrivateKey, nameof(settings.PrivateKey));
        AddIfMissing(missing, settings.ShareableLink, nameof(settings.ShareableLink));

        if (missing.Count > 0)
        {
            return ValidateOptionsResult.Fail(
                $"Enabled Dojah configuration is missing: {string.Join(", ", missing)}.");
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
}
