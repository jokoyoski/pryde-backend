using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Entities;

namespace Pryde.Services.Service.Interface;

public interface IWalletService
{
    Task<Wallet> CreateWalletForUserAsync(
        User user,
        string accountName,
        CancellationToken cancellationToken = default);

    Task<FundVirtualAccountResponseDto> FundVirtualAccountAsync(
        FundVirtualAccountRequestDto request,
        CancellationToken cancellationToken = default);
}
