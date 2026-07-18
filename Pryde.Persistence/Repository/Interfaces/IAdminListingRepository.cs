using Pryde.Domain.Entities;
using Pryde.Domain.Enums;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IAdminListingRepository
{
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetUsersAsync(string? role, UserStatus? status, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<KycVerification> Items, int TotalCount)> GetKycAsync(KycStatus? status, string? role, string? provider, string? search, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> GetVehiclesAsync(bool? isActive, Guid? ownerId, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<VehicleDocument> Items, int TotalCount)> GetVehicleDocumentsAsync(Guid? vehicleId, Guid? ownerId, VehicleDocumentType? documentType, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetStaffAsync(string? search, string? role, UserStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminStaffSummary> GetStaffSummaryAsync(CancellationToken cancellationToken = default);
    Task<User?> GetUserDetailsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetDriversAsync(string? search, UserStatus? status, KycStatus? kycStatus, VehicleDocumentReviewStatus? documentStatus, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminDriverTripSummary> GetDriverTripSummaryAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<KycVerification?> GetKycDetailsAsync(Guid kycId, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetVehicleDetailsAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<AdminDashboardCounts> GetDashboardCountsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetRecentDriverRequestsAsync(int count, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<WalletTransaction> Items, int TotalCount)> GetWalletTransactionsAsync(Guid? userId, WalletTransactionType? transactionType, string? status, DateTime? dateFrom, DateTime? dateTo, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WalletTransaction>> GetRecentWalletTransactionsAsync(int count, CancellationToken cancellationToken = default);
}

public sealed record AdminStaffSummary(int TotalStaff, int ActiveStaff, int InactiveStaff, int PendingInvites);
public sealed record AdminDriverTripSummary(int TotalTrips, int ScheduledTrips, int CompletedTrips);
public sealed record AdminDashboardCounts(
    int TotalUsers,
    int TotalDrivers,
    int ActiveDrivers,
    int PendingDriverRequests,
    int PendingKycRequests,
    int PendingVehicleDocumentRequests,
    int TotalTransactions);
