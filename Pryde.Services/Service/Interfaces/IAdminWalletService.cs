using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IAdminWalletService
{
    Task<AdminFundWalletResponseDto> FundWalletAsync(
        AdminFundWalletRequest request,
        CancellationToken cancellationToken = default);
}
