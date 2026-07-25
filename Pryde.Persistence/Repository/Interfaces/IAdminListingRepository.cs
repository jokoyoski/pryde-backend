using Pryde.Domain.Entities;
using Pryde.Domain.Enums;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IAdminListingRepository
{
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetUsersAsync(string? role, UserStatus? status, string? search, bool? isActive, bool? isEmailVerified, bool? isPhoneVerified, KycStatus? kycStatus, DateTime? createdFrom, DateTime? createdTo, string? sortBy, string? sortDirection, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<KycVerification> Items, int TotalCount)> GetKycAsync(KycStatus? status, string? role, string? provider, string? search, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> GetVehiclesAsync(VehicleOnboardingStatus? onboardingStatus, bool? isActive, Guid? ownerId, VehicleRegistrationType? registrationType, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<VehicleDocument> Items, int TotalCount)> GetVehicleDocumentsAsync(Guid? vehicleId, Guid? ownerId, VehicleDocumentType? documentType, VehicleDocumentReviewStatus? reviewStatus, DateTime? expiryFrom, DateTime? expiryTo, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Trip> Items, int TotalCount)> GetTripsAsync(string? search, Guid? driverId, TripStatus? status, DateTime? departureFrom, DateTime? departureTo, bool? isRecurring, bool? isActive, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<Trip?> GetTripAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<TripBooking> Items, int TotalCount)> GetBookingsAsync(Guid? userId, Guid? driverId, Guid? tripId, BookingStatus? status, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<TripBooking?> GetBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetStaffAsync(string? search, string? role, UserStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminStaffSummary> GetStaffSummaryAsync(CancellationToken cancellationToken = default);
    Task<User?> GetUserDetailsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetDriversAsync(string? search, UserStatus? status, KycStatus? kycStatus, VehicleDocumentReviewStatus? documentStatus, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminDriverTripSummary> GetDriverTripSummaryAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<KycVerification?> GetKycDetailsAsync(Guid kycId, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetVehicleDetailsAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<AdminDashboardCounts> GetDashboardCountsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetRecentDriverRequestsAsync(int count, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<WalletTransaction> Items, int TotalCount)> GetWalletTransactionsAsync(Guid? userId, WalletTransactionType? transactionType, string? status, DateTime? dateFrom, DateTime? dateTo, string? reference, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
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
