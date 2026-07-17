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
        var query = context.Users.AsNoTracking().AsQueryable();
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
        KycStatus? status, string? role, string? search, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.KycVerifications.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(kyc => kyc.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(role)) query = ApplyRoleFilter(query, role);
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
