using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IDriverWithdrawalService
{
    Task<DriverWithdrawalOtpResponseDto> RequestOtpAsync(
        Guid userId,
        DriverWithdrawalOtpRequestDto request,
        CancellationToken cancellationToken = default);

    Task<DriverWithdrawalResponseDto> CreateAsync(
        Guid userId,
        CreateDriverWithdrawalRequestDto request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverWithdrawalResponseDto>> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<DriverWithdrawalResponseDto> GetByIdAsync(
        Guid userId,
        Guid withdrawalId,
        CancellationToken cancellationToken = default);
}
