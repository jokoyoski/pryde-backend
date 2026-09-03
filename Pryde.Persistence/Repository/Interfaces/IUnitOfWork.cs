using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IUnitOfWork
{
    IUserRepository Users { get; }
    IRoleRepository Roles { get; }
    IUserRoleRepository UserRoles { get; }
    IProfileRepository Profiles { get; }
    IKycVerificationRepository KycVerifications { get; }
    IKycVerificationAttemptRepository KycVerificationAttempts { get; }
    IVehicleRepository Vehicles { get; }
    IVehicleDocumentRepository VehicleDocuments { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    IPasswordResetCodeRepository PasswordResetCodes { get; }
    IVerificationCodeRepository VerificationCodes { get; }
    ITripRepository Trips { get; }
    ITripBookingRepository TripBookings { get; }
    IBookingChatRepository BookingChats { get; }
    IRecurringTripRepository RecurringTrips { get; }
    ITripSubscriptionRepository TripSubscriptions { get; }
    ISavedRecurringTripRepository SavedRecurringTrips { get; }
    IWalletRepository Wallets { get; }
    IWalletTransactionRepository WalletTransactions { get; }
    IPaystackWalletFundingRequestRepository PaystackWalletFundingRequests { get; }
    IVehicleImageRepository VehicleImages { get; }
    IVehicleAmenityRepository VehicleAmenities { get; }
    IAdminListingRepository AdminListings { get; }
    IEscrowRepository Escrows { get; }
    ILedgerRepository Ledger { get; }
    IDriverBankAccountRepository DriverBankAccounts { get; }
    INotificationRepository Notifications { get; }
    ITripRatingRepository TripRatings { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);
    Task<T> ExecuteInTransactionOnceAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);
    void ClearTracking();
}
