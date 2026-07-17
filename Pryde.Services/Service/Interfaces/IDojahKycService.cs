using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IDojahKycService
{
    Task<DojahKycConfigResponseDto> GetConfigAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task ProcessWebhookAsync(
        ReadOnlyMemory<byte> payload,
        string? signature,
        CancellationToken cancellationToken = default);
}
