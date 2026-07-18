using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class AdminListingRepository(PrydeDbContext context) : IAdminListingRepository
{
    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetUsersAsync(
        string? role, UserStatus? status, string? search, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Users.AsNoTracking()
            .Where(user => !user.UserRoles.Any(userRole =>
                userRole.Role.Name == "Admin" || userRole.Role.Name == "SuperAdmin"));
        query = ApplyUserFilters(query, role, status, search);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(user => user.Profile)
            .Include(user => user.KycVerification)
            .Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id)
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
        bool? isActive, Guid? ownerId, string? search, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Vehicles.AsNoTracking().AsQueryable();
        if (isActive.HasValue) query = query.Where(vehicle => vehicle.IsActive == isActive.Value);
        if (ownerId.HasValue) query = query.Where(vehicle => vehicle.UserId == ownerId.Value);
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
            .Include(vehicle => vehicle.Documents)
            .OrderByDescending(vehicle => vehicle.CreatedAt).ThenBy(vehicle => vehicle.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<VehicleDocument> Items, int TotalCount)> GetVehicleDocumentsAsync(
        Guid? vehicleId, Guid? ownerId, VehicleDocumentType? documentType, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.VehicleDocuments.AsNoTracking().AsQueryable();
        if (vehicleId.HasValue) query = query.Where(document => document.VehicleId == vehicleId.Value);
        if (ownerId.HasValue) query = query.Where(document => document.Vehicle.UserId == ownerId.Value);
        if (documentType.HasValue) query = query.Where(document => document.DocumentType == documentType.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(document => document.Vehicle).ThenInclude(vehicle => vehicle.User)
            .OrderByDescending(document => document.CreatedAt).ThenBy(document => document.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

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
            .Include(user => user.Vehicles).ThenInclude(vehicle => vehicle.Documents)
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetDriversAsync(
        string? search, UserStatus? status, KycStatus? kycStatus,
        VehicleDocumentReviewStatus? documentStatus, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Users.AsNoTracking()
            .Where(user => user.UserRoles.Any(userRole => userRole.Role.Name == "Driver"));
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
            .Include(vehicle => vehicle.Documents)
            .FirstOrDefaultAsync(vehicle => vehicle.Id == vehicleId, cancellationToken);
    }

    public async Task<AdminDashboardCounts> GetDashboardCountsAsync(
        CancellationToken cancellationToken = default)
    {
        var users = context.Users.AsNoTracking();
        var drivers = users.Where(user => user.UserRoles.Any(userRole => userRole.Role.Name == "Driver"));
        var customerUsers = users.Where(user => !user.UserRoles.Any(userRole =>
            userRole.Role.Name == "Admin" || userRole.Role.Name == "SuperAdmin"));

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
            .Where(user => user.UserRoles.Any(userRole => userRole.Role.Name == "Driver"))
            .OrderByDescending(user => user.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<WalletTransaction> Items, int TotalCount)> GetWalletTransactionsAsync(
        Guid? userId, WalletTransactionType? transactionType, string? status,
        DateTime? dateFrom, DateTime? dateTo, string? search, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.WalletTransactions.AsNoTracking().AsQueryable();
        if (userId.HasValue) query = query.Where(transaction => transaction.Wallet.UserId == userId.Value);
        if (transactionType.HasValue) query = query.Where(transaction => transaction.Type == transactionType.Value);
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            query = query.Where(_ => false);
        if (dateFrom.HasValue) query = query.Where(transaction => transaction.CreatedAt >= dateFrom.Value.ToUniversalTime());
        if (dateTo.HasValue) query = query.Where(transaction => transaction.CreatedAt <= dateTo.Value.ToUniversalTime());
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
            userRole.Role.Name == "Admin" || userRole.Role.Name == "SuperAdmin"));

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
            ? query.Where(user => user.UserRoles.Any(x => x.Role.Name == "Driver") && user.UserRoles.Any(x => x.Role.Name == "Passenger"))
            : query.Where(user => user.UserRoles.Any(x => x.Role.Name.ToLower() == role.Trim().ToLower()));
    }

    private static IQueryable<KycVerification> ApplyRoleFilter(IQueryable<KycVerification> query, string role)
    {
        return role.Equals("Both", StringComparison.OrdinalIgnoreCase)
            ? query.Where(kyc => kyc.User.UserRoles.Any(x => x.Role.Name == "Driver") && kyc.User.UserRoles.Any(x => x.Role.Name == "Passenger"))
            : query.Where(kyc => kyc.User.UserRoles.Any(x => x.Role.Name.ToLower() == role.Trim().ToLower()));
    }
}
