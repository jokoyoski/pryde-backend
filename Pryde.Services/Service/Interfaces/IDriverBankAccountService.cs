using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IDriverBankAccountService
{
    Task<IReadOnlyList<BankResponseDto>> GetBanksAsync(
        CancellationToken cancellationToken = default);

    Task<ResolvedBankAccountResponseDto> ResolveAccountAsync(
        ResolveBankAccountRequestDto request,
        CancellationToken cancellationToken = default);

    Task<DriverBankAccountResponseDto> CreateAsync(
        Guid userId,
        CreateDriverBankAccountRequestDto request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverBankAccountResponseDto>> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
