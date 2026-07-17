using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Tests.TestInfrastructure;

internal sealed class TestUnitOfWork : IUnitOfWork
{
    public TestUnitOfWork()
    {
        Trips = new TestTripRepository();
        TripBookings = new TestTripBookingRepository((TestTripRepository)Trips);
        Vehicles = new TestVehicleRepository();
        UserRoles = new TestUserRoleRepository();
        Profiles = new TestProfileRepository();
        Wallets = new TestWalletRepository();
        WalletTransactions = new TestWalletTransactionRepository();
        VirtualAccounts = new TestVirtualAccountRepository();
        AdminListings = new TestAdminListingRepository();
        KycVerifications = new TestKycVerificationRepository();
    }

    public TestTripRepository TripRepository => (TestTripRepository)Trips;
    public TestTripBookingRepository TripBookingRepository => (TestTripBookingRepository)TripBookings;
    public TestVehicleRepository VehicleRepository => (TestVehicleRepository)Vehicles;
    public TestUserRoleRepository UserRoleRepository => (TestUserRoleRepository)UserRoles;
    public TestProfileRepository ProfileRepository => (TestProfileRepository)Profiles;
    public TestWalletRepository WalletRepository => (TestWalletRepository)Wallets;
    public TestWalletTransactionRepository WalletTransactionRepository => (TestWalletTransactionRepository)WalletTransactions;
    public TestVirtualAccountRepository VirtualAccountRepository => (TestVirtualAccountRepository)VirtualAccounts;
    public TestAdminListingRepository AdminListingRepository => (TestAdminListingRepository)AdminListings;
    public TestKycVerificationRepository KycVerificationRepository => (TestKycVerificationRepository)KycVerifications;
    public int SaveChangesCount { get; private set; }

    public IUserRepository Users { get; } = null!;
    public IRoleRepository Roles { get; } = null!;
    public IUserRoleRepository UserRoles { get; }
    public IProfileRepository Profiles { get; }
    public IKycVerificationRepository KycVerifications { get; }
    public IVehicleRepository Vehicles { get; }
    public IVehicleDocumentRepository VehicleDocuments { get; } = null!;
    public IRefreshTokenRepository RefreshTokens { get; } = null!;
    public IPasswordResetCodeRepository PasswordResetCodes { get; } = null!;
    public ITripRepository Trips { get; }
    public ITripBookingRepository TripBookings { get; }
    public IRecurringTripRepository RecurringTrips { get; } = null!;
    public ITripSubscriptionRepository TripSubscriptions { get; } = null!;
    public IWalletRepository Wallets { get; }
    public IWalletTransactionRepository WalletTransactions { get; }
    public IVirtualAccountRepository VirtualAccounts { get; }
    public IVehicleImageRepository VehicleImages { get; } = null!;
    public IAdminListingRepository AdminListings { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        return Task.FromResult(1);
    }
}

internal sealed class TestKycVerificationRepository : IKycVerificationRepository
{
    public List<KycVerification> Items { get; } = [];

    public Task<KycVerification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(kyc => kyc.Id == id));
    public Task<KycVerification?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(kyc => kyc.UserId == userId));
    public Task<KycVerification?> GetByProviderReferenceAsync(string providerReference, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(kyc => kyc.ProviderReference == providerReference));
    public Task<IReadOnlyList<KycVerification>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<KycVerification>>(Items.ToList());
    public Task<IReadOnlyList<KycVerification>> GetByStatusAsync(KycStatus status, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<KycVerification>>(Items.Where(kyc => kyc.Status == status).ToList());
    public Task<bool> ExistsForUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Any(kyc => kyc.UserId == userId));
    public Task<KycVerification> CreateAsync(KycVerification kycVerification, CancellationToken cancellationToken = default) { Items.Add(kycVerification); return Task.FromResult(kycVerification); }
    public void Update(KycVerification kycVerification) { }
    public void Delete(KycVerification kycVerification) => Items.Remove(kycVerification);
}

internal sealed class TestWalletRepository : IWalletRepository
{
    public List<Wallet> Items { get; } = [];
    public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(wallet => wallet.UserId == userId));
    public Task<Wallet> CreateAsync(Wallet wallet, CancellationToken cancellationToken = default) { Items.Add(wallet); return Task.FromResult(wallet); }
    public void Update(Wallet wallet) { }
}

internal sealed class TestWalletTransactionRepository : IWalletTransactionRepository
{
    public List<WalletTransaction> Items { get; } = [];
    public Task<WalletTransaction> CreateAsync(WalletTransaction transaction, CancellationToken cancellationToken = default) { Items.Add(transaction); return Task.FromResult(transaction); }
    public Task<IReadOnlyList<WalletTransaction>> GetByWalletIdAsync(Guid walletId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WalletTransaction>>(Items.Where(transaction => transaction.WalletId == walletId).OrderByDescending(transaction => transaction.CreatedAt).ToList());
}

internal sealed class TestVirtualAccountRepository : IVirtualAccountRepository
{
    public List<VirtualAccount> Items { get; } = [];
    public Task<VirtualAccount?> GetByWalletIdAsync(Guid walletId, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(account => account.WalletId == walletId));
    public Task<VirtualAccount?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(account => account.AccountNumber == accountNumber));
    public Task<bool> ExistsByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default) => Task.FromResult(Items.Any(account => account.AccountNumber == accountNumber));
    public Task<VirtualAccount> CreateAsync(VirtualAccount virtualAccount, CancellationToken cancellationToken = default) { Items.Add(virtualAccount); return Task.FromResult(virtualAccount); }
}

internal sealed class TestAdminListingRepository : IAdminListingRepository
{
    public List<User> Users { get; } = [];
    public List<KycVerification> Kyc { get; } = [];
    public List<Vehicle> Vehicles { get; } = [];
    public List<VehicleDocument> VehicleDocuments { get; } = [];

    public Task<(IReadOnlyList<User> Items, int TotalCount)> GetUsersAsync(string? role, UserStatus? status, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(Users.Where(user => !status.HasValue || user.Status == status.Value), pageNumber, pageSize);
    public Task<(IReadOnlyList<KycVerification> Items, int TotalCount)> GetKycAsync(KycStatus? status, string? role, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(Kyc.Where(kyc => !status.HasValue || kyc.Status == status.Value), pageNumber, pageSize);
    public Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> GetVehiclesAsync(bool? isActive, Guid? ownerId, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(Vehicles.Where(vehicle => (!isActive.HasValue || vehicle.IsActive == isActive.Value) && (!ownerId.HasValue || vehicle.UserId == ownerId.Value)), pageNumber, pageSize);
    public Task<(IReadOnlyList<VehicleDocument> Items, int TotalCount)> GetVehicleDocumentsAsync(Guid? vehicleId, Guid? ownerId, VehicleDocumentType? documentType, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(VehicleDocuments.Where(document => (!vehicleId.HasValue || document.VehicleId == vehicleId.Value) && (!ownerId.HasValue || document.Vehicle.UserId == ownerId.Value) && (!documentType.HasValue || document.DocumentType == documentType.Value)), pageNumber, pageSize);

    private static Task<(IReadOnlyList<T> Items, int TotalCount)> Page<T>(IEnumerable<T> source, int pageNumber, int pageSize)
    {
        var all = source.ToList();
        return Task.FromResult<(IReadOnlyList<T>, int)>((all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(), all.Count));
    }
}

internal sealed class TestTripRepository : ITripRepository
{
    public List<Trip> Items { get; } = [];
    public User? DefaultDriver { get; set; }
    public Vehicle? DefaultVehicle { get; set; }

    public Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(t => t.Id == id));

    public Task<Trip?> GetByIdWithBookingsAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<Trip?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<Trip?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Trip>> GetByDriverIdAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Trip>>(Items.Where(t => t.DriverId == driverId).ToList());

    public Task<IReadOnlyList<Trip>> SearchAsync(
        DateTime utcNow, DateTime? departureDate, bool? requiresLuggage,
        int requiredSeats, CancellationToken cancellationToken = default)
    {
        var query = Items.Where(t => t.Status == TripStatus.Scheduled
            && t.DepartureTime > utcNow
            && t.DepartureTime - TimeSpan.FromHours(t.BookingWindowHours) > utcNow
            && t.AvailableSeats >= requiredSeats);
        if (departureDate.HasValue)
            query = query.Where(t => t.DepartureTime.Date == departureDate.Value.Date);
        if (requiresLuggage == true)
            query = query.Where(t => t.AllowLuggage);
        return Task.FromResult<IReadOnlyList<Trip>>(query.OrderBy(t => t.DepartureTime).ToList());
    }

    public Task<IReadOnlyList<Trip>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Trip>>(Items.Where(t => t.Status == TripStatus.Scheduled && t.AvailableSeats > 0).ToList());

    public Task<Trip> CreateAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        trip.Driver = DefaultDriver ?? new User { Id = trip.DriverId };
        trip.Vehicle = DefaultVehicle ?? new Vehicle { Id = trip.VehicleId };
        Items.Add(trip);
        return Task.FromResult(trip);
    }

    public void Update(Trip trip) { }
    public void Delete(Trip trip) => Items.Remove(trip);
}

internal sealed class TestTripBookingRepository(TestTripRepository trips) : ITripBookingRepository
{
    public List<TripBooking> Items { get; } = [];

    public Task<TripBooking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(b => b.Id == id));

    public Task<TripBooking?> GetByIdWithTripAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<TripBooking>> GetByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TripBooking>>(Items.Where(b => b.TripId == tripId).ToList());

    public Task<IReadOnlyList<TripBooking>> GetPendingByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TripBooking>>(Items.Where(b => b.TripId == tripId && b.Status == BookingStatus.Pending).ToList());

    public Task<IReadOnlyList<TripBooking>> GetApprovedByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TripBooking>>(Items.Where(b => b.TripId == tripId && b.Status == BookingStatus.Approved).ToList());

    public Task<IReadOnlyList<TripBooking>> GetByPassengerIdAsync(Guid passengerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TripBooking>>(Items.Where(b => b.PassengerId == passengerId).ToList());

    public Task<bool> HasActiveBookingAsync(Guid tripId, Guid passengerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(b => b.TripId == tripId && b.PassengerId == passengerId
            && b.Status is BookingStatus.Pending or BookingStatus.Approved));

    public Task<TripBooking> CreateAsync(TripBooking booking, CancellationToken cancellationToken = default)
    {
        booking.Trip = trips.Items.Single(t => t.Id == booking.TripId);
        booking.Passenger = new User { Id = booking.PassengerId };
        booking.Trip.Bookings.Add(booking);
        Items.Add(booking);
        return Task.FromResult(booking);
    }

    public void Update(TripBooking booking) { }
}

internal sealed class TestVehicleRepository : IVehicleRepository
{
    public List<Vehicle> Items { get; } = [];
    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(v => v.Id == id));
    public Task<Vehicle?> GetByIdWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);
    public Task<IReadOnlyList<Vehicle>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Vehicle>>(Items.Where(v => v.UserId == userId).ToList());
    public Task<Vehicle?> GetByLicensePlateAsync(string licensePlateNumber, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(v => v.LicensePlateNumber == licensePlateNumber));
    public Task<bool> ExistsAsync(string licensePlateNumber, CancellationToken cancellationToken = default) => Task.FromResult(Items.Any(v => v.LicensePlateNumber == licensePlateNumber));
    public Task<Vehicle> CreateAsync(Vehicle vehicle, CancellationToken cancellationToken = default) { Items.Add(vehicle); return Task.FromResult(vehicle); }
    public void Update(Vehicle vehicle) { }
    public void Delete(Vehicle vehicle) => Items.Remove(vehicle);
}

internal sealed class TestUserRoleRepository : IUserRoleRepository
{
    public List<UserRole> Items { get; } = [];
    public Task<IReadOnlyList<UserRole>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UserRole>>(Items.Where(r => r.UserId == userId).ToList());
    public Task<bool> ExistsAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Any(r => r.UserId == userId && r.RoleId == roleId));
    public Task<UserRole> CreateAsync(UserRole userRole, CancellationToken cancellationToken = default) { Items.Add(userRole); return Task.FromResult(userRole); }
    public void Delete(UserRole userRole) => Items.Remove(userRole);
}

internal sealed class TestProfileRepository : IProfileRepository
{
    public List<Profile> Items { get; } = [];
    public Task<Profile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(p => p.Id == id));
    public Task<Profile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(p => p.UserId == userId));
    public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Any(p => p.UserId == userId));
    public Task<Profile> CreateAsync(Profile profile, CancellationToken cancellationToken = default) { Items.Add(profile); return Task.FromResult(profile); }
    public void Update(Profile profile) { }
    public void Delete(Profile profile) => Items.Remove(profile);
}
