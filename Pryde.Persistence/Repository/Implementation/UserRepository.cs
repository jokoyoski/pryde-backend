using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class UserRepository(PrydeDbContext context) : IUserRepository
{
    public async Task<bool> ExistsByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await context.Users.AnyAsync(
            user => user.Id == userId,
            cancellationToken);
    }

    public async Task<User?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Id == id,
                cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        email = NormalizeEmail(email);

        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Email == email,
                cancellationToken);
    }

    public async Task<User?> GetByPhoneNumberAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        phoneNumber = NormalizePhoneNumber(phoneNumber);

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return null;
        }

        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.PhoneNumber == phoneNumber,
                cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AsNoTracking()
            .OrderByDescending(user => user.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>>
        GetActiveNotificationRecipientIdsAsync(
            string? role,
            CancellationToken cancellationToken = default)
    {
        var query = context.Users
            .AsNoTracking()
            .Where(user =>
                user.Status != UserStatus.Suspended &&
                user.Status != UserStatus.Deactivated &&
                !user.UserRoles.Any(userRole =>
                    userRole.Role.Name == RoleNames.Admin ||
                    userRole.Role.Name == RoleNames.SuperAdmin) &&
                user.UserRoles.Any(userRole =>
                    userRole.Role.Name == RoleNames.Driver ||
                    userRole.Role.Name == RoleNames.Passenger));

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(user =>
                user.UserRoles.Any(userRole =>
                    userRole.Role.Name == role));
        }

        return await query
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        string email,
        string? phoneNumber,
        CancellationToken cancellationToken = default)
    {
        email = NormalizeEmail(email);
        phoneNumber = NormalizePhoneNumber(phoneNumber);

        return await context.Users.AnyAsync(
            user => user.Email == email ||
                   (!string.IsNullOrWhiteSpace(phoneNumber) &&
                    user.PhoneNumber == phoneNumber),
            cancellationToken);
    }

    public async Task<bool> HasProtectedDeletionRecordsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var walletIds = context.Wallets
            .Where(wallet => wallet.UserId == userId)
            .Select(wallet => wallet.Id);
        var relatedBookingIds = context.TripBookings
            .Where(booking =>
                booking.PassengerId == userId ||
                booking.Trip.DriverId == userId)
            .Select(booking => booking.Id);

        return await context.Wallets.AnyAsync(
                   wallet => wallet.UserId == userId &&
                             (wallet.Balance != 0 || wallet.EscrowBalance != 0),
                   cancellationToken) ||
               await context.WalletTransactions.AnyAsync(
                   transaction => walletIds.Contains(transaction.WalletId),
                   cancellationToken) ||
               await context.LedgerAccounts.AnyAsync(
                   account => account.WalletId.HasValue &&
                              walletIds.Contains(account.WalletId.Value),
                   cancellationToken) ||
               await context.Escrows.AnyAsync(
                   escrow => escrow.PassengerId == userId ||
                             escrow.DriverId == userId ||
                             relatedBookingIds.Contains(escrow.BookingId),
                   cancellationToken) ||
               await context.LedgerTransactions.AnyAsync(
                   transaction => transaction.BookingId.HasValue &&
                                  relatedBookingIds.Contains(transaction.BookingId.Value),
                   cancellationToken) ||
               await context.TripBookings.AnyAsync(
                   booking =>
                       (booking.PassengerId == userId ||
                        booking.Trip.DriverId == userId) &&
                       (booking.PaidAt.HasValue ||
                        booking.Status == BookingStatus.Completed),
                   cancellationToken) ||
               await context.Trips.AnyAsync(
                   trip => trip.DriverId == userId &&
                           trip.Status == TripStatus.Completed,
                   cancellationToken) ||
               await context.TripRatings.AnyAsync(
                   rating => rating.RaterId == userId ||
                             rating.RatedUserId == userId ||
                             relatedBookingIds.Contains(rating.BookingId),
                   cancellationToken);
    }

    public async Task DeleteWithRelatedDataAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var vehicleIds = context.Vehicles
            .Where(vehicle => vehicle.UserId == userId)
            .Select(vehicle => vehicle.Id);
        var recurringTripIds = context.RecurringTrips
            .Where(trip => trip.DriverId == userId)
            .Select(trip => trip.Id);
        var tripIds = context.Trips
            .Where(trip => trip.DriverId == userId)
            .Select(trip => trip.Id);
        var bookingIds = context.TripBookings
            .Where(booking =>
                booking.PassengerId == userId ||
                tripIds.Contains(booking.TripId))
            .Select(booking => booking.Id);
        var walletIds = context.Wallets
            .Where(wallet => wallet.UserId == userId)
            .Select(wallet => wallet.Id);

        await context.VehicleDocuments
            .Where(document => document.ReviewedBy == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    document => document.ReviewedBy,
                    (Guid?)null),
                cancellationToken);
        await context.TripRatings
            .Where(rating => rating.RaterId == userId ||
                             rating.RatedUserId == userId ||
                             bookingIds.Contains(rating.BookingId))
            .ExecuteDeleteAsync(cancellationToken);
        await context.TripSubscriptions
            .Where(subscription => subscription.PassengerId == userId ||
                                   recurringTripIds.Contains(subscription.RecurringTripId))
            .ExecuteDeleteAsync(cancellationToken);
        await context.TripBookings
            .Where(booking => bookingIds.Contains(booking.Id))
            .ExecuteDeleteAsync(cancellationToken);
        await context.Trips
            .Where(trip => trip.DriverId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await context.RecurringTrips
            .Where(trip => trip.DriverId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await context.VehicleAmenities
            .Where(amenity => vehicleIds.Contains(amenity.VehicleId))
            .ExecuteDeleteAsync(cancellationToken);
        await context.VehicleImages
            .Where(image => vehicleIds.Contains(image.VehicleId))
            .ExecuteDeleteAsync(cancellationToken);
        await context.VehicleDocuments
            .Where(document => vehicleIds.Contains(document.VehicleId))
            .ExecuteDeleteAsync(cancellationToken);
        await context.Vehicles
            .Where(vehicle => vehicle.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await context.VirtualAccounts
            .Where(account => walletIds.Contains(account.WalletId))
            .ExecuteDeleteAsync(cancellationToken);
        await context.Wallets
            .Where(wallet => wallet.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await context.Notifications
            .Where(notification => notification.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await context.DriverBankAccounts
            .Where(account => account.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await context.VerificationCodes
            .Where(code => code.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await context.PasswordResetCodes
            .Where(code => code.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await context.RefreshTokens
            .Where(token => token.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await context.KycVerifications
            .Where(kyc => kyc.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await context.Profiles
            .Where(profile => profile.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await context.UserRoles
            .Where(userRole => userRole.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await context.Users
            .Where(user => user.Id == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<User> CreateAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        user.Email = NormalizeEmail(user.Email);
        user.PhoneNumber = NormalizePhoneNumber(user.PhoneNumber);

        await context.Users.AddAsync(user, cancellationToken);
        return user;
    }

    public void Update(User user)
    {
        user.Email = NormalizeEmail(user.Email);
        user.PhoneNumber = NormalizePhoneNumber(user.PhoneNumber);

        context.Users.Update(user);
    }

    public void Delete(User user)
    {
        context.Users.Remove(user);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string? NormalizePhoneNumber(string? phoneNumber)
    {
        return phoneNumber?.Trim();
    }
}
