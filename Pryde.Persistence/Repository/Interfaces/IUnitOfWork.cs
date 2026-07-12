using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IUnitOfWork
{
    IUserRepository Users { get; }
    IRoleRepository Roles { get; }
    IUserRoleRepository UserRoles { get; }
    IProfileRepository Profiles { get; }
    IKycVerificationRepository KycVerifications { get; }
    IVehicleRepository Vehicles { get; }
    IVehicleDocumentRepository VehicleDocuments { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    IPasswordResetCodeRepository PasswordResetCodes { get; }
    ITripRepository Trips { get; }
    ITripBookingRepository TripBookings { get; }
    IRecurringTripRepository RecurringTrips { get; }
    ITripSubscriptionRepository TripSubscriptions { get; }
    IWalletRepository Wallets { get; }
    IWalletTransactionRepository WalletTransactions { get; }
    IVirtualAccountRepository VirtualAccounts { get; }
    IVehicleImageRepository VehicleImages { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
