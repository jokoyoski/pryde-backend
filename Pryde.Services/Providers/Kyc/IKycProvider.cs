namespace Pryde.Services.Providers.Kyc;

public interface IKycProvider
{
    string Name { get; }

    Task<KycProviderResult> CreateSessionAsync(
        KycProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<KycProviderResult> RetryAsync(
        KycProviderRequest request,
        CancellationToken cancellationToken = default);
}
