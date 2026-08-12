using Pryde.Services.Providers.Kyc;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public sealed class KycProviderService(IKycProviderResolver resolver) : IKycProviderService
{
    public Task<KycProviderResult> CreateSessionAsync(Guid userId, CancellationToken cancellationToken = default) =>
        resolver.ResolveActive().CreateSessionAsync(new KycProviderRequest(userId), cancellationToken);

    public Task<KycProviderResult> RetryAsync(Guid userId, CancellationToken cancellationToken = default) =>
        resolver.ResolveActive().RetryAsync(new KycProviderRequest(userId), cancellationToken);
}
