using Microsoft.Extensions.Options;
using Pryde.Domain.Common.Exceptions;
using Pryde.Services.Settings;

namespace Pryde.Services.Providers.Kyc;

public sealed class KycProviderResolver(
    IEnumerable<IKycProvider> providers,
    IOptions<KycSettings> options) : IKycProviderResolver
{
    private readonly IReadOnlyDictionary<string, IKycProvider> _providers =
        providers.ToDictionary(provider => provider.Name, StringComparer.OrdinalIgnoreCase);

    public IKycProvider ResolveActive()
    {
        var providerName = string.IsNullOrWhiteSpace(options.Value.ActiveProvider)
            ? KycSettings.DefaultProvider
            : options.Value.ActiveProvider.Trim();

        return Resolve(providerName);
    }

    public IKycProvider Resolve(string providerName)
    {
        if (_providers.TryGetValue(providerName, out var provider))
        {
            return provider;
        }

        throw new ServiceUnavailableException(
            $"KYC provider '{providerName}' is not registered.");
    }
}
