using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class AdminListingRepository(PrydeDbContext context) : IAdminListingRepository
{
    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetUsersAsync(
        string? role, UserStatus? status, string? search, bool? isActive,
        bool? isEmailVerified, bool? isPhoneVerified, KycStatus? kycStatus,
        DateTime? createdFrom, DateTime? createdTo, string? sortBy, string? sortDirection,
        int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Users.AsNoTracking()
            .Where(user => !user.UserRoles.Any(userRole =>
                userRole.Role.Name == RoleNames.Admin || userRole.Role.Name == RoleNames.SuperAdmin));
        query = ApplyUserFilters(query, role, status, search);
        if (isActive.HasValue)
            query = query.Where(user => (user.Status == UserStatus.Active) == isActive.Value);
        if (isEmailVerified.HasValue)
            query = query.Where(user => user.IsEmailVerified == isEmailVerified.Value);
        if (isPhoneVerified.HasValue)
            query = query.Where(user => user.IsPhoneNumberVerified == isPhoneVerified.Value);
        if (kycStatus.HasValue)
            query = query.Where(user =>
                user.KycVerification != null && user.KycVerification.Status == kycStatus.Value);
        if (createdFrom.HasValue)
            query = query.Where(user => user.CreatedAt >= createdFrom.Value.ToUniversalTime());
        if (createdTo.HasValue)
            query = query.Where(user => user.CreatedAt <= createdTo.Value.ToUniversalTime());

        var totalCount = await query.CountAsync(cancellationToken);
        var sortedQuery = ApplyUserSorting(query, sortBy, sortDirection);
        var items = await sortedQuery
            .Include(user => user.Profile)
            .Include(user => user.KycVerification)
            .Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<KycVerification> Items, int TotalCount)> GetKycAsync(
        KycStatus? status, string? role, string? provider, string? search,
        DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.KycVerifications.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(kyc => kyc.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(role)) query = ApplyRoleFilter(query, role);
        if (!string.IsNullOrWhiteSpace(provider))
        {
            var providerName = provider.Trim().ToLower();
            query = query.Where(kyc => kyc.ProviderName != null && kyc.ProviderName.ToLower() == providerName);
        }
        if (dateFrom.HasValue) query = query.Where(kyc => kyc.CreatedAt >= dateFrom.Value.ToUniversalTime());
        if (dateTo.HasValue) query = query.Where(kyc => kyc.CreatedAt <= dateTo.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(kyc =>
                kyc.User.Email.ToLower().Contains(term) ||
                kyc.User.PhoneNumber.ToLower().Contains(term) ||
                (kyc.User.Profile != null &&
                 (kyc.User.Profile.FirstName.ToLower().Contains(term) || kyc.User.Profile.LastName.ToLower().Contains(term))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(kyc => kyc.User).ThenInclude(user => user.Profile)
            .Include(kyc => kyc.User).ThenInclude(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .OrderByDescending(kyc => kyc.CreatedAt).ThenBy(kyc => kyc.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> GetVehiclesAsync(
        VehicleOnboardingStatus? onboardingStatus, bool? isActive, Guid? ownerId,
        VehicleRegistrationType? registrationType, string? search, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Vehicles.AsNoTracking().AsQueryable();
        if (onboardingStatus.HasValue)
            query = query.Where(vehicle => vehicle.OnboardingStatus == onboardingStatus.Value);
        if (isActive.HasValue) query = query.Where(vehicle => vehicle.IsActive == isActive.Value);
        if (ownerId.HasValue) query = query.Where(vehicle => vehicle.UserId == ownerId.Value);
        if (registrationType.HasValue)
            query = query.Where(vehicle => vehicle.RegistrationType == registrationType.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(vehicle =>
                vehicle.LicensePlateNumber.ToLower().Contains(term) ||
                vehicle.User.Email.ToLower().Contains(term) ||
                (vehicle.User.Profile != null &&
                 (vehicle.User.Profile.FirstName.ToLower().Contains(term) || vehicle.User.Profile.LastName.ToLower().Contains(term))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(vehicle => vehicle.User).ThenInclude(user => user.Profile)
            .Include(vehicle => vehicle.Images)
            .Include(vehicle => vehicle.Amenities)
            .Include(vehicle => vehicle.Documents)
            .OrderByDescending(vehicle => vehicle.CreatedAt).ThenBy(vehicle => vehicle.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<VehicleDocument> Items, int TotalCount)> GetVehicleDocumentsAsync(
        Guid? vehicleId, Guid? ownerId, VehicleDocumentType? documentType,
        VehicleDocumentReviewStatus? reviewStatus, DateTime? expiryFrom, DateTime? expiryTo,
        int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.VehicleDocuments.AsNoTracking().AsQueryable();
        if (vehicleId.HasValue) query = query.Where(document => document.VehicleId == vehicleId.Value);
        if (ownerId.HasValue) query = query.Where(document => document.Vehicle.UserId == ownerId.Value);
        if (documentType.HasValue) query = query.Where(document => document.DocumentType == documentType.Value);
        if (reviewStatus.HasValue)
            query = query.Where(document => document.ReviewStatus == reviewStatus.Value);
        if (expiryFrom.HasValue)
            query = query.Where(document => document.ExpiryDate >= expiryFrom.Value.ToUniversalTime());
        if (expiryTo.HasValue)
            query = query.Where(document => document.ExpiryDate <= expiryTo.Value.ToUniversalTime());

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(document => document.Vehicle).ThenInclude(vehicle => vehicle.User)
            .OrderByDescending(document => document.CreatedAt).ThenBy(document => document.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<Trip> Items, int TotalCount)> GetTripsAsync(
        string? search, Guid? driverId, TripStatus? status, DateTime? departureFrom,
        DateTime? departureTo, bool? isRecurring, bool? isActive, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Trips.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(trip =>
                trip.OriginAddress.ToLower().Contains(term) ||
                trip.DestinationAddress.ToLower().Contains(term) ||
                trip.Vehicle.LicensePlateNumber.ToLower().Contains(term) ||
                trip.Driver.Email.ToLower().Contains(term) ||
                (trip.Driver.Profile != null &&
                 (trip.Driver.Profile.FirstName.ToLower().Contains(term) ||
                  trip.Driver.Profile.LastName.ToLower().Contains(term))));
        }
        if (driverId.HasValue) query = query.Where(trip => trip.DriverId == driverId.Value);
        if (status.HasValue) query = query.Where(trip => trip.Status == status.Value);
        if (departureFrom.HasValue)
            query = query.Where(trip => trip.DepartureTime >= departureFrom.Value.ToUniversalTime());
        if (departureTo.HasValue)
            query = query.Where(trip => trip.DepartureTime <= departureTo.Value.ToUniversalTime());
        if (isRecurring.HasValue)
            query = query.Where(trip => trip.RecurringTripId.HasValue == isRecurring.Value);
        if (isActive.HasValue)
            query = query.Where(trip =>
                (trip.Status != TripStatus.Completed && trip.Status != TripStatus.Cancelled) == isActive.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(trip => trip.Driver).ThenInclude(driver => driver.Profile)
            .Include(trip => trip.Vehicle).ThenInclude(vehicle => vehicle.Images)
            .OrderByDescending(trip => trip.CreatedAt).ThenBy(trip => trip.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public Task<Trip?> GetTripAsync(
        Guid tripId, CancellationToken cancellationToken = default) =>
        context.Trips.AsNoTracking()
            .Include(trip => trip.Driver).ThenInclude(driver => driver.Profile)
            .Include(trip => trip.Vehicle).ThenInclude(vehicle => vehicle.Images)
            .Include(trip => trip.Bookings)
            .FirstOrDefaultAsync(trip => trip.Id == tripId, cancellationToken);

    public async Task<(IReadOnlyList<TripBooking> Items, int TotalCount)> GetBookingsAsync(
        Guid? userId, Guid? driverId, Guid? tripId, BookingStatus? status,
        DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.TripBookings.AsNoTracking().AsQueryable();
        if (userId.HasValue) query = query.Where(booking => booking.PassengerId == userId.Value);
        if (driverId.HasValue) query = query.Where(booking => booking.Trip.DriverId == driverId.Value);
        if (tripId.HasValue) query = query.Where(booking => booking.TripId == tripId.Value);
        if (status.HasValue) query = query.Where(booking => booking.Status == status.Value);
        if (dateFrom.HasValue)
            query = query.Where(booking => booking.RequestedAt >= dateFrom.Value.ToUniversalTime());
        if (dateTo.HasValue)
            query = query.Where(booking => booking.RequestedAt <= dateTo.Value.ToUniversalTime());

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(booking => booking.Passenger).ThenInclude(passenger => passenger.Profile)
            .Include(booking => booking.Trip)
            .OrderByDescending(booking => booking.CreatedAt).ThenBy(booking => booking.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public Task<TripBooking?> GetBookingAsync(
        Guid bookingId, CancellationToken cancellationToken = default) =>
        context.TripBookings.AsNoTracking()
            .Include(booking => booking.Passenger).ThenInclude(passenger => passenger.Profile)
            .Include(booking => booking.Trip)
            .FirstOrDefaultAsync(booking => booking.Id == bookingId, cancellationToken);

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetStaffAsync(
        string? search, string? role, UserStatus? status, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = StaffQuery();
        if (status.HasValue) query = query.Where(user => user.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(role)) query = ApplyRoleFilter(query, role);
        if (!string.IsNullOrWhiteSpace(search)) query = ApplySearch(query, search);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(user => user.Profile)
            .Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<AdminStaffSummary> GetStaffSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var query = StaffQuery();
        return new AdminStaffSummary(
            await query.CountAsync(cancellationToken),
            await query.CountAsync(user => user.Status == UserStatus.Active, cancellationToken),
            await query.CountAsync(user => user.Status == UserStatus.Suspended || user.Status == UserStatus.Deactivated, cancellationToken),
            await query.CountAsync(user => user.Status == UserStatus.Pending, cancellationToken));
    }

    public async Task<User?> GetUserDetailsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AsNoTracking()
            .Include(user => user.Profile)
            .Include(user => user.KycVerification)
            .Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .Include(user => user.Vehicles).ThenInclude(vehicle => vehicle.Images)
            .Include(user => user.Vehicles).ThenInclude(vehicle => vehicle.Amenities)
            .Include(user => user.Vehicles).ThenInclude(vehicle => vehicle.Documents)
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetDriversAsync(
        string? search, UserStatus? status, KycStatus? kycStatus,
        VehicleDocumentReviewStatus? documentStatus, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Users.AsNoTracking()
            .Where(user => user.UserRoles.Any(userRole => userRole.Role.Name == RoleNames.Driver));
        if (status.HasValue) query = query.Where(user => user.Status == status.Value);
        if (kycStatus.HasValue)
            query = query.Where(user => user.KycVerification != null && user.KycVerification.Status == kycStatus.Value);
        if (documentStatus.HasValue)
            query = query.Where(user => user.Vehicles.Any(vehicle =>
                vehicle.Documents.Any(document => document.ReviewStatus == documentStatus.Value)));
        if (!string.IsNullOrWhiteSpace(search)) query = ApplySearch(query, search);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(user => user.Profile)
            .Include(user => user.KycVerification)
            .Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .Include(user => user.Vehicles).ThenInclude(vehicle => vehicle.Images)
            .Include(user => user.Vehicles).ThenInclude(vehicle => vehicle.Amenities)
            .Include(user => user.Vehicles).ThenInclude(vehicle => vehicle.Documents)
            .OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<AdminDriverTripSummary> GetDriverTripSummaryAsync(
        Guid driverId, CancellationToken cancellationToken = default)
    {
        var query = context.Trips.AsNoTracking().Where(trip => trip.DriverId == driverId);
        return new AdminDriverTripSummary(
            await query.CountAsync(cancellationToken),
            await query.CountAsync(trip => trip.Status == TripStatus.Scheduled, cancellationToken),
            await query.CountAsync(trip => trip.Status == TripStatus.Completed, cancellationToken));
    }

    public async Task<KycVerification?> GetKycDetailsAsync(
        Guid kycId, CancellationToken cancellationToken = default)
    {
        return await context.KycVerifications.AsNoTracking()
            .Include(kyc => kyc.User).ThenInclude(user => user.Profile)
            .Include(kyc => kyc.User).ThenInclude(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(kyc => kyc.Id == kycId, cancellationToken);
    }

    public async Task<Vehicle?> GetVehicleDetailsAsync(
        Guid vehicleId, CancellationToken cancellationToken = default)
    {
        return await context.Vehicles.AsNoTracking()
            .Include(vehicle => vehicle.User).ThenInclude(user => user.Profile)
            .Include(vehicle => vehicle.Images)
            .Include(vehicle => vehicle.Amenities)
            .Include(vehicle => vehicle.Documents)
            .FirstOrDefaultAsync(vehicle => vehicle.Id == vehicleId, cancellationToken);
    }

    public async Task<AdminDashboardCounts> GetDashboardCountsAsync(
        CancellationToken cancellationToken = default)
    {
        var users = context.Users.AsNoTracking();
        var drivers = users.Where(user =>
            user.UserRoles.Any(userRole => userRole.Role.Name == RoleNames.Driver));
        var customerUsers = users.Where(user => !user.UserRoles.Any(userRole =>
            userRole.Role.Name == RoleNames.Admin || userRole.Role.Name == RoleNames.SuperAdmin));

        return new AdminDashboardCounts(
            await customerUsers.CountAsync(cancellationToken),
            await drivers.CountAsync(cancellationToken),
            await drivers.CountAsync(user => user.Status == UserStatus.Active, cancellationToken),
            await drivers.CountAsync(user => user.Status == UserStatus.Pending, cancellationToken),
            await context.KycVerifications.CountAsync(kyc =>
                kyc.Status == KycStatus.Pending || kyc.Status == KycStatus.Submitted, cancellationToken),
            await context.VehicleDocuments.CountAsync(document =>
                document.ReviewStatus == VehicleDocumentReviewStatus.Pending, cancellationToken),
            await context.WalletTransactions.CountAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<User>> GetRecentDriverRequestsAsync(
        int count, CancellationToken cancellationToken = default)
    {
        return await context.Users.AsNoTracking()
            .Include(user => user.Profile)
            .Include(user => user.KycVerification)
            .Where(user => user.UserRoles.Any(userRole => userRole.Role.Name == RoleNames.Driver))
            .OrderByDescending(user => user.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<WalletTransaction> Items, int TotalCount)> GetWalletTransactionsAsync(
        Guid? userId, WalletTransactionType? transactionType, string? status,
        DateTime? dateFrom, DateTime? dateTo, string? reference, string? search,
        int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.WalletTransactions.AsNoTracking().AsQueryable();
        if (userId.HasValue) query = query.Where(transaction => transaction.Wallet.UserId == userId.Value);
        if (transactionType.HasValue) query = query.Where(transaction => transaction.Type == transactionType.Value);
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            query = query.Where(_ => false);
        if (dateFrom.HasValue) query = query.Where(transaction => transaction.CreatedAt >= dateFrom.Value.ToUniversalTime());
        if (dateTo.HasValue) query = query.Where(transaction => transaction.CreatedAt <= dateTo.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(reference))
        {
            var referenceTerm = reference.Trim().ToLower();
            query = query.Where(transaction =>
                transaction.Reference != null && transaction.Reference.ToLower().Contains(referenceTerm));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(transaction =>
                (transaction.Reference != null && transaction.Reference.ToLower().Contains(term)) ||
                transaction.Wallet.User.Email.ToLower().Contains(term) ||
                (transaction.Wallet.User.Profile != null &&
                 (transaction.Wallet.User.Profile.FirstName.ToLower().Contains(term) ||
                  transaction.Wallet.User.Profile.LastName.ToLower().Contains(term))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(transaction => transaction.Wallet).ThenInclude(wallet => wallet.User).ThenInclude(user => user.Profile)
            .OrderByDescending(transaction => transaction.CreatedAt).ThenBy(transaction => transaction.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<IReadOnlyList<WalletTransaction>> GetRecentWalletTransactionsAsync(
        int count, CancellationToken cancellationToken = default)
    {
        return await context.WalletTransactions.AsNoTracking()
            .Include(transaction => transaction.Wallet).ThenInclude(wallet => wallet.User).ThenInclude(user => user.Profile)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<User> ApplyUserFilters(IQueryable<User> query, string? role, UserStatus? status, string? search)
    {
        if (status.HasValue) query = query.Where(user => user.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(role)) query = ApplyRoleFilter(query, role);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(user =>
                user.Email.ToLower().Contains(term) || user.PhoneNumber.ToLower().Contains(term) ||
                (user.Profile != null &&
                 (user.Profile.FirstName.ToLower().Contains(term) || user.Profile.LastName.ToLower().Contains(term))));
        }
        return query;
    }

    private IQueryable<User> StaffQuery() => context.Users.AsNoTracking()
        .Where(user => user.UserRoles.Any(userRole =>
            userRole.Role.Name == RoleNames.Admin || userRole.Role.Name == RoleNames.SuperAdmin));

    private static IOrderedQueryable<User> ApplyUserSorting(
        IQueryable<User> query, string? sortBy, string? sortDirection)
    {
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var field = sortBy?.Trim().ToLowerInvariant();
        return (field, descending) switch
        {
            ("email", false) => query.OrderBy(user => user.Email).ThenBy(user => user.Id),
            ("email", true) => query.OrderByDescending(user => user.Email).ThenBy(user => user.Id),
            ("status", false) => query.OrderBy(user => user.Status).ThenBy(user => user.Id),
            ("status", true) => query.OrderByDescending(user => user.Status).ThenBy(user => user.Id),
            ("firstname", false) => query.OrderBy(user => user.Profile!.FirstName).ThenBy(user => user.Id),
            ("firstname", true) => query.OrderByDescending(user => user.Profile!.FirstName).ThenBy(user => user.Id),
            ("lastname", false) => query.OrderBy(user => user.Profile!.LastName).ThenBy(user => user.Id),
            ("lastname", true) => query.OrderByDescending(user => user.Profile!.LastName).ThenBy(user => user.Id),
            (_, false) => query.OrderBy(user => user.CreatedAt).ThenBy(user => user.Id),
            _ => query.OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id)
        };
    }

    private static IQueryable<User> ApplySearch(IQueryable<User> query, string search)
    {
        var term = search.Trim().ToLower();
        return query.Where(user =>
            user.Email.ToLower().Contains(term) || user.PhoneNumber.ToLower().Contains(term) ||
            (user.Profile != null &&
             (user.Profile.FirstName.ToLower().Contains(term) || user.Profile.LastName.ToLower().Contains(term))));
    }

    private static IQueryable<User> ApplyRoleFilter(IQueryable<User> query, string role)
    {
        return role.Equals("Both", StringComparison.OrdinalIgnoreCase)
            ? query.Where(user => user.UserRoles.Any(x => x.Role.Name == RoleNames.Driver) &&
                                  user.UserRoles.Any(x => x.Role.Name == RoleNames.Passenger))
            : query.Where(user => user.UserRoles.Any(x => x.Role.Name.ToLower() == role.Trim().ToLower()));
    }

    private static IQueryable<KycVerification> ApplyRoleFilter(IQueryable<KycVerification> query, string role)
    {
        return role.Equals("Both", StringComparison.OrdinalIgnoreCase)
            ? query.Where(kyc => kyc.User.UserRoles.Any(x => x.Role.Name == RoleNames.Driver) &&
                                 kyc.User.UserRoles.Any(x => x.Role.Name == RoleNames.Passenger))
            : query.Where(kyc => kyc.User.UserRoles.Any(x => x.Role.Name.ToLower() == role.Trim().ToLower()));
    }
}
