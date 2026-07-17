using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class UnitOfWork(
    PrydeDbContext context,
    IUserRepository users,
    IRoleRepository roles,
    IUserRoleRepository userRoles,
    IProfileRepository profiles,
    IKycVerificationRepository kycVerifications,
    IVehicleRepository vehicles,
    IVehicleDocumentRepository vehicleDocuments,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordResetCodeRepository passwordResetCodes,
    ITripBookingRepository tripBooking,
    ITripRepository trip,
    IRecurringTripRepository recurringTrip,
    ITripSubscriptionRepository tripSubscription,
    IWalletRepository wallet,
    IWalletTransactionRepository walletTransaction,
    IVirtualAccountRepository virtualAccount,
    IVehicleImageRepository vehicleImage,
    IAdminListingRepository adminListings)
    : IUnitOfWork
{
    public IUserRepository Users { get; } = users;
    public IRoleRepository Roles { get; } = roles;
    public IUserRoleRepository UserRoles { get; } = userRoles;
    public IProfileRepository Profiles { get; } = profiles;
    public IKycVerificationRepository KycVerifications { get; } = kycVerifications;
    public IVehicleRepository Vehicles { get; } = vehicles;
    public IVehicleDocumentRepository VehicleDocuments { get; } = vehicleDocuments;
    public IRefreshTokenRepository RefreshTokens { get; } = refreshTokenRepository;
    public IPasswordResetCodeRepository PasswordResetCodes { get; } = passwordResetCodes;
    public ITripBookingRepository TripBookings { get; } = tripBooking;
    public ITripRepository Trips { get; } = trip;
    public IRecurringTripRepository RecurringTrips { get; } = recurringTrip;
    public ITripSubscriptionRepository TripSubscriptions { get; } = tripSubscription;
    public IWalletRepository Wallets { get; } = wallet;
    public IWalletTransactionRepository WalletTransactions { get; } = walletTransaction;
    public IVirtualAccountRepository VirtualAccounts { get; } = virtualAccount;
    public IVehicleImageRepository VehicleImages { get; } = vehicleImage;
    public IAdminListingRepository AdminListings { get; } = adminListings;


    public async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }
}
