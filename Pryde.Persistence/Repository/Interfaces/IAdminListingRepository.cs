using Pryde.Domain.Entities;
using Pryde.Domain.Enums;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IAdminListingRepository
{
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetUsersAsync(string? role, UserStatus? status, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<KycVerification> Items, int TotalCount)> GetKycAsync(KycStatus? status, string? role, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> GetVehiclesAsync(bool? isActive, Guid? ownerId, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<VehicleDocument> Items, int TotalCount)> GetVehicleDocumentsAsync(Guid? vehicleId, Guid? ownerId, VehicleDocumentType? documentType, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
