using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Enums;

namespace Pryde.Services.Service.Interface;
public interface IVehicleService
{
    Task<VehicleResponseDto> CreateAsync(Guid driverId, string licensePlateNumber, int capacity, List<string> imageUrls, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VehicleResponseDto>> GetMyVehiclesAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> UpdateAsync(Guid vehicleId, Guid requestingUserId, int capacity, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> UpdateDetailsAsync(Guid vehicleId, Guid requestingUserId, VehicleDetailsRequestDto request, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> UploadMediaAsync(Guid vehicleId, Guid requestingUserId, VehicleMediaRequestDto request, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> UpdateMediaAsync(Guid vehicleId, Guid requestingUserId, IReadOnlyDictionary<VehicleImageType, string> imageUrls, string? walkAroundVideoUrl, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> UpdateCapacityExtrasAsync(Guid vehicleId, Guid requestingUserId, VehicleCapacityExtrasRequestDto request, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> SubmitAsync(Guid vehicleId, Guid requestingUserId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid vehicleId, Guid requestingUserId, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> AddImagesAsync(Guid vehicleId, Guid requestingUserId, List<string> imageUrls, CancellationToken cancellationToken = default);
    Task DeleteImageAsync(Guid vehicleId, Guid imageId, Guid requestingUserId, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> ActivateAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> DeactivateAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> RejectAsync(Guid vehicleId, string reason, CancellationToken cancellationToken = default);
    Task<VehicleResponseDto> RejectDriverApplicationAsync(Guid driverId, string reason, CancellationToken cancellationToken = default);
}
