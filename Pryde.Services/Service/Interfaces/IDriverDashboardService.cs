using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IDriverDashboardService
{
    Task<DriverDashboardResponseDto> GetAsync(
        Guid driverId,
        CancellationToken cancellationToken = default);
}
