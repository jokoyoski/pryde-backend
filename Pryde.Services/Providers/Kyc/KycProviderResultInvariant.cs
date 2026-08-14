using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Enums;

namespace Pryde.Services.Providers.Kyc;

public static class KycProviderResultInvariant
{
    public static void Ensure(
        string expectedProvider,
        KycProviderResult result)
    {
        var actionable = !string.IsNullOrWhiteSpace(result.SessionUrl) ||
                         result.Sessions.Count > 0;
        if (!string.Equals(
                expectedProvider,
                result.Provider,
                StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidResponse();
        }

        if (string.Equals(
                result.Provider,
                "Dojah",
                StringComparison.OrdinalIgnoreCase) &&
            ((!string.IsNullOrWhiteSpace(result.IntegrationType) &&
              !string.Equals(result.IntegrationType, "Widget", StringComparison.Ordinal)) ||
             (actionable &&
              (!string.Equals(result.IntegrationType, "Widget", StringComparison.Ordinal) ||
               string.IsNullOrWhiteSpace(result.SessionUrl) ||
               !HasClientValue(result, "appId") ||
               !HasClientValue(result, "publicKey") ||
               !HasClientValue(result, "widgetId"))) ||
             result.Sessions.Count > 0 ||
             IsSmileReference(result.Reference) ||
             ContainsSmileData(result.SessionUrl) ||
             result.ClientConfiguration.Any(pair =>
                 ContainsSmileData(pair.Key) ||
                 ContainsSmileData(pair.Value)) ||
             result.Metadata.Any(pair =>
                 ContainsSmileData(pair.Key) ||
                 ContainsSmileData(pair.Value))))
        {
            throw InvalidResponse();
        }

        if (string.Equals(
                result.Provider,
                "SmileId",
                StringComparison.OrdinalIgnoreCase) &&
            ((!string.IsNullOrWhiteSpace(result.IntegrationType) &&
              !string.Equals(result.IntegrationType, "HostedRedirect", StringComparison.Ordinal)) ||
             (actionable &&
              (!string.Equals(result.IntegrationType, "HostedRedirect", StringComparison.Ordinal) ||
               !string.IsNullOrWhiteSpace(result.SessionUrl) ||
               result.Sessions.Count == 0 ||
               result.Sessions.Any(session =>
                   string.IsNullOrWhiteSpace(session.VerificationUrl)))) ||
             (!string.IsNullOrWhiteSpace(result.Reference) &&
              !IsSmileReference(result.Reference))))
        {
            throw InvalidResponse();
        }

    }

    private static bool HasClientValue(
        KycProviderResult result,
        string key) =>
        result.ClientConfiguration.TryGetValue(key, out var value) &&
        !string.IsNullOrWhiteSpace(value);

    private static bool IsSmileReference(string? value) =>
        value?.StartsWith(
            "SMILE-GROUP-",
            StringComparison.OrdinalIgnoreCase) == true;

    private static bool ContainsSmileData(string? value) =>
        value?.Contains(
            "SMILE-GROUP-",
            StringComparison.OrdinalIgnoreCase) == true ||
        value?.Contains(
            "usesmileid.com",
            StringComparison.OrdinalIgnoreCase) == true ||
        value?.Contains(
            "smileId",
            StringComparison.OrdinalIgnoreCase) == true;

    private static ServiceUnavailableException InvalidResponse() =>
        new("The selected KYC provider returned an inconsistent session response.");
}
