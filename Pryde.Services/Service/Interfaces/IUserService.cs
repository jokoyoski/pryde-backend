using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IUserService
{
    Task<IReadOnlyList<UserSummaryResponseDto>> GetAllAsync(
        CancellationToken cancellationToken = default);
}