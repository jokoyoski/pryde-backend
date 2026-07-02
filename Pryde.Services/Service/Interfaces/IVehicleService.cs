using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
namespace Pryde.Services.Service.Interface;
public interface IVehicleService
{
    Task<VehicleResponseDto> CreateAsync(Guid driverId, string licensePlateNumber, int capacity, string vehicleImageUrl, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VehicleResponseDto>> GetMyVehiclesAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> UpdateAsync(Guid vehicleId, Guid requestingUserId, int capacity, string vehicleImageUrl, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid vehicleId, Guid requestingUserId, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> ActivateAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> DeactivateAsync(Guid vehicleId, CancellationToken cancellationToken = default);
}