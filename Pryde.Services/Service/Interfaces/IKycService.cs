using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IKycService
{
    Task<KycVerificationResponseDto> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<KycVerificationResponseDto> ApproveAsync(
        Guid userId, CancellationToken cancellationToken = default);
    Task<KycVerificationResponseDto> RejectAsync(
        Guid userId, string reason, CancellationToken cancellationToken = default);
}
