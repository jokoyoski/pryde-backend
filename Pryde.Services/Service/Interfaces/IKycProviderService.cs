using Pryde.Services.Providers.Kyc;

namespace Pryde.Services.Service.Interface;

public interface IKycProviderService
{
    Task<KycProviderResult> CreateSessionAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<KycProviderResult> CreateSessionAsync(
        Guid userId,
        string? selectedIdType,
        CancellationToken cancellationToken = default);
    Task<KycProviderResult> RetryAsync(Guid userId, CancellationToken cancellationToken = default);
}
