using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class UnitOfWork(
    PrydeDbContext context,
    IUserRepository users,
    IRoleRepository roles,
    IUserRoleRepository userRoles,
    IProfileRepository profiles,
    IKycVerificationRepository kycVerifications,
    IKycVerificationAttemptRepository kycVerificationAttempts,
    IVehicleRepository vehicles,
    IVehicleDocumentRepository vehicleDocuments,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordResetCodeRepository passwordResetCodes,
    IVerificationCodeRepository verificationCodes,
    ITripBookingRepository tripBooking,
    ITripRepository trip,
    IRecurringTripRepository recurringTrip,
    ITripSubscriptionRepository tripSubscription,
    ISavedRecurringTripRepository savedRecurringTrip,
    IWalletRepository wallet,
    IWalletTransactionRepository walletTransaction,
    IPaystackWalletFundingRequestRepository paystackFundingRequest,
    IVehicleImageRepository vehicleImage,
    IVehicleAmenityRepository vehicleAmenity,
    IAdminListingRepository adminListings,
    IEscrowRepository escrows,
    ILedgerRepository ledger,
    IDriverBankAccountRepository driverBankAccounts,
    INotificationRepository notifications,
    ITripRatingRepository tripRatings)
    : IUnitOfWork
{
    public IUserRepository Users { get; } = users;
    public IRoleRepository Roles { get; } = roles;
    public IUserRoleRepository UserRoles { get; } = userRoles;
    public IProfileRepository Profiles { get; } = profiles;
    public IKycVerificationRepository KycVerifications { get; } = kycVerifications;
    public IKycVerificationAttemptRepository KycVerificationAttempts { get; } = kycVerificationAttempts;
    public IVehicleRepository Vehicles { get; } = vehicles;
    public IVehicleDocumentRepository VehicleDocuments { get; } = vehicleDocuments;
    public IRefreshTokenRepository RefreshTokens { get; } = refreshTokenRepository;
    public IPasswordResetCodeRepository PasswordResetCodes { get; } = passwordResetCodes;
    public IVerificationCodeRepository VerificationCodes { get; } = verificationCodes;
    public ITripBookingRepository TripBookings { get; } = tripBooking;
    public ITripRepository Trips { get; } = trip;
    public IRecurringTripRepository RecurringTrips { get; } = recurringTrip;
    public ITripSubscriptionRepository TripSubscriptions { get; } = tripSubscription;
    public ISavedRecurringTripRepository SavedRecurringTrips { get; } = savedRecurringTrip;
    public IWalletRepository Wallets { get; } = wallet;
    public IWalletTransactionRepository WalletTransactions { get; } = walletTransaction;
    public IPaystackWalletFundingRequestRepository PaystackWalletFundingRequests { get; } =
        paystackFundingRequest;
    public IVehicleImageRepository VehicleImages { get; } = vehicleImage;
    public IVehicleAmenityRepository VehicleAmenities { get; } = vehicleAmenity;
    public IAdminListingRepository AdminListings { get; } = adminListings;
    public IEscrowRepository Escrows { get; } = escrows;
    public ILedgerRepository Ledger { get; } = ledger;
    public IDriverBankAccountRepository DriverBankAccounts { get; } = driverBankAccounts;
    public INotificationRepository Notifications { get; } =
        notifications;
    public ITripRatingRepository TripRatings { get; } = tripRatings;


    public async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken);
            try
            {
                var result = await action(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<T> ExecuteInTransactionOnceAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);
            try
            {
                var result = await action(cancellationToken);
                await transaction.CommitAsync(
                    cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(
                    cancellationToken);
                throw;
            }
        });
    }

    public void ClearTracking()
    {
        context.ChangeTracker.Clear();
    }
}
