using Pryde.Domain.Enums;

namespace Pryde.Services.Providers.Kyc;

public sealed record KycProviderRequest(Guid UserId);

public sealed class KycProviderResult
{
    public string Provider { get; init; } = string.Empty;
    public string? IntegrationType { get; init; }
    public string Reference { get; init; } = string.Empty;
    public KycProviderStatus Status { get; init; }
    public string? SessionUrl { get; init; }
    public IReadOnlyDictionary<string, string> ClientConfiguration { get; init; } =
        new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();
    public IReadOnlyList<KycProviderSession> Sessions { get; init; } = [];
}

public sealed class KycProviderSession
{
    public string Flow { get; init; } = string.Empty;
    public string JobId { get; init; } = string.Empty;
    public string? VerificationUrl { get; init; }
    public bool Required { get; init; }
    public string Status { get; init; } = string.Empty;
}
