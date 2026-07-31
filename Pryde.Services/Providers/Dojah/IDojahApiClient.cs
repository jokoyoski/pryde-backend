using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Providers.Dojah;

public interface IDojahApiClient
{
    Task<DojahVerificationDetailsResponseDto> GetVerificationAsync(
        string dojahReference,
        CancellationToken cancellationToken = default);
}
