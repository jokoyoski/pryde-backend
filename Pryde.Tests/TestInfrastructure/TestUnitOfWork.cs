using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Tests.TestInfrastructure;

internal sealed class TestUnitOfWork : IUnitOfWork
{
    public TestUnitOfWork()
    {
        AdminListings = new TestAdminListingRepository();
        Users = new TestUserRepository(((TestAdminListingRepository)AdminListings).Users);
        Roles = new TestRoleRepository();
        Trips = new TestTripRepository();
        TripBookings = new TestTripBookingRepository((TestTripRepository)Trips);
        Vehicles = new TestVehicleRepository();
        UserRoles = new TestUserRoleRepository(((TestUserRepository)Users).Items, ((TestRoleRepository)Roles).Items);
        Profiles = new TestProfileRepository(((TestUserRepository)Users).Items);
        VehicleDocuments = new TestVehicleDocumentRepository();
        Wallets = new TestWalletRepository();
        WalletTransactions = new TestWalletTransactionRepository();
        VirtualAccounts = new TestVirtualAccountRepository();
        KycVerifications = new TestKycVerificationRepository();
        PasswordResetCodes = new TestPasswordResetCodeRepository();
        VerificationCodes = new TestVerificationCodeRepository();
        RefreshTokens = new TestRefreshTokenRepository();
        Escrows = new TestEscrowRepository((TestTripBookingRepository)TripBookings);
        Ledger = new TestLedgerRepository();
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
    public TestEscrowRepository EscrowRepository => (TestEscrowRepository)Escrows;
    public TestLedgerRepository LedgerRepository => (TestLedgerRepository)Ledger;
    public TestVerificationCodeRepository VerificationCodeRepository =>
        (TestVerificationCodeRepository)VerificationCodes;
    public int SaveChangesCount { get; private set; }

    public IUserRepository Users { get; }
    public IRoleRepository Roles { get; }
    public IUserRoleRepository UserRoles { get; }
    public IProfileRepository Profiles { get; }
    public IKycVerificationRepository KycVerifications { get; }
    public IVehicleRepository Vehicles { get; }
    public IVehicleDocumentRepository VehicleDocuments { get; }
    public IRefreshTokenRepository RefreshTokens { get; }
    public IPasswordResetCodeRepository PasswordResetCodes { get; }
    public IVerificationCodeRepository VerificationCodes { get; }
    public ITripRepository Trips { get; }
    public ITripBookingRepository TripBookings { get; }
    public IRecurringTripRepository RecurringTrips { get; } = null!;
    public ITripSubscriptionRepository TripSubscriptions { get; } = null!;
    public IWalletRepository Wallets { get; }
    public IWalletTransactionRepository WalletTransactions { get; }
    public IVirtualAccountRepository VirtualAccounts { get; }
    public IVehicleImageRepository VehicleImages { get; } = null!;
    public IAdminListingRepository AdminListings { get; }
    public IEscrowRepository Escrows { get; }
    public ILedgerRepository Ledger { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        return Task.FromResult(1);
    }

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default) => action(cancellationToken);
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

internal sealed class TestUserRepository(List<User> items) : IUserRepository
{
    public List<User> Items { get; } = items;
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(user => user.Id == id));
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(user => user.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
    public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(user => user.PhoneNumber == phoneNumber));
    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>(Items.ToList());
    public Task<bool> ExistsAsync(string email, string? phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult(Items.Any(user => user.Email.Equals(email, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrWhiteSpace(phoneNumber) && user.PhoneNumber == phoneNumber)));
    public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default) { Items.Add(user); return Task.FromResult(user); }
    public void Update(User user) { }
    public void Delete(User user) => Items.Remove(user);
}

internal sealed class TestRoleRepository : IRoleRepository
{
    public List<Role> Items { get; } = [new Role { Name = "Admin" }, new Role { Name = "SuperAdmin" }, new Role { Name = "Driver" }, new Role { Name = "Passenger" }];
    public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(role => role.Id == id));
    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(role => role.Name == name));
    public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Role>>(Items.ToList());
    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.Any(role => role.Id == id));
    public Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default) { Items.Add(role); return Task.FromResult(role); }
    public void Update(Role role) { }
    public void Delete(Role role) => Items.Remove(role);
}

internal sealed class TestPasswordResetCodeRepository : IPasswordResetCodeRepository
{
    public List<PasswordResetCode> Items { get; } = [];
    public Task<PasswordResetCode?> GetLatestActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Where(code => code.UserId == userId && code.UsedAt == null).OrderByDescending(code => code.CreatedAt).FirstOrDefault());
    public Task<PasswordResetCode> CreateAsync(PasswordResetCode code, CancellationToken cancellationToken = default) { Items.Add(code); return Task.FromResult(code); }
    public void MarkUsed(PasswordResetCode code) => code.UsedAt = DateTime.UtcNow;
    public Task InvalidateAllForUserAsync(Guid userId, CancellationToken cancellationToken = default) { foreach (var code in Items.Where(code => code.UserId == userId && code.UsedAt == null)) code.UsedAt = DateTime.UtcNow; return Task.CompletedTask; }
}

internal sealed class TestRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Items { get; } = [];
    public Task<RefreshToken?> GetByTokenHashAsync(
        string tokenHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(token => token.TokenHash == tokenHash));
    public Task<RefreshToken> CreateAsync(
        RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        Items.Add(refreshToken);
        return Task.FromResult(refreshToken);
    }
    public void Revoke(RefreshToken refreshToken) => refreshToken.RevokedAt = DateTime.UtcNow;
    public Task RevokeAllActiveForUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        foreach (var token in Items.Where(token => token.UserId == userId && token.IsActive))
            token.RevokedAt = DateTime.UtcNow;
        return Task.CompletedTask;
    }
}

internal sealed class TestVerificationCodeRepository : IVerificationCodeRepository
{
    public List<VerificationCode> Items { get; } = [];

    public Task<VerificationCode?> GetLatestAsync(
        Guid userId, VerificationCodePurpose purpose, VerificationChannel channel,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(code => code.UserId == userId &&
                                           code.Purpose == purpose &&
                                           code.Channel == channel)
            .OrderByDescending(code => code.CreatedAt)
            .FirstOrDefault());

    public Task<int> CountCreatedSinceAsync(
        Guid userId, VerificationCodePurpose purpose, VerificationChannel channel,
        DateTime createdSince, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Count(code => code.UserId == userId &&
                                           code.Purpose == purpose &&
                                           code.Channel == channel &&
                                           code.CreatedAt >= createdSince));

    public Task InvalidateUnusedAsync(
        Guid userId, VerificationCodePurpose purpose, VerificationChannel channel,
        DateTime consumedAt, CancellationToken cancellationToken = default)
    {
        foreach (var code in Items.Where(code => code.UserId == userId &&
                                                 code.Purpose == purpose &&
                                                 code.Channel == channel &&
                                                 code.ConsumedAt == null))
        {
            code.ConsumedAt = consumedAt;
        }

        return Task.CompletedTask;
    }

    public Task<VerificationCode> CreateAsync(
        VerificationCode verificationCode, CancellationToken cancellationToken = default)
    {
        Items.Add(verificationCode);
        return Task.FromResult(verificationCode);
    }

    public void Update(VerificationCode verificationCode) { }
}

internal sealed class TestVehicleDocumentRepository : IVehicleDocumentRepository
{
    public List<VehicleDocument> Items { get; } = [];
    public Task<VehicleDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(document => document.Id == id));
    public Task<IReadOnlyList<VehicleDocument>> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<VehicleDocument>>(Items.Where(document => document.VehicleId == vehicleId).ToList());
    public Task<IReadOnlyList<VehicleDocument>> GetExpiringBeforeAsync(DateTime threshold, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<VehicleDocument>>(Items.Where(document => document.ExpiryDate <= threshold).ToList());
    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.Any(document => document.Id == id));
    public Task<VehicleDocument> CreateAsync(VehicleDocument vehicleDocument, CancellationToken cancellationToken = default) { Items.Add(vehicleDocument); return Task.FromResult(vehicleDocument); }
    public void Update(VehicleDocument vehicleDocument) { }
    public void Delete(VehicleDocument vehicleDocument) => Items.Remove(vehicleDocument);
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
    public List<WalletTransaction> WalletTransactions { get; } = [];

    public Task<(IReadOnlyList<User> Items, int TotalCount)> GetUsersAsync(string? role, UserStatus? status, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(Users.Where(user => !status.HasValue || user.Status == status.Value), pageNumber, pageSize);
    public Task<(IReadOnlyList<KycVerification> Items, int TotalCount)> GetKycAsync(KycStatus? status, string? role, string? provider, string? search, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(Kyc.Where(kyc => (!status.HasValue || kyc.Status == status.Value) && (string.IsNullOrWhiteSpace(provider) || kyc.ProviderName == provider)), pageNumber, pageSize);
    public Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> GetVehiclesAsync(bool? isActive, Guid? ownerId, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(Vehicles.Where(vehicle => (!isActive.HasValue || vehicle.IsActive == isActive.Value) && (!ownerId.HasValue || vehicle.UserId == ownerId.Value)), pageNumber, pageSize);
    public Task<(IReadOnlyList<VehicleDocument> Items, int TotalCount)> GetVehicleDocumentsAsync(Guid? vehicleId, Guid? ownerId, VehicleDocumentType? documentType, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(VehicleDocuments.Where(document => (!vehicleId.HasValue || document.VehicleId == vehicleId.Value) && (!ownerId.HasValue || document.Vehicle.UserId == ownerId.Value) && (!documentType.HasValue || document.DocumentType == documentType.Value)), pageNumber, pageSize);
    public Task<(IReadOnlyList<User> Items, int TotalCount)> GetStaffAsync(string? search, string? role, UserStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(Users.Where(user => user.UserRoles.Any(userRole => userRole.Role.Name is "Admin" or "SuperAdmin") && (!status.HasValue || user.Status == status.Value) && (string.IsNullOrWhiteSpace(role) || user.UserRoles.Any(userRole => userRole.Role.Name.Equals(role, StringComparison.OrdinalIgnoreCase)))), pageNumber, pageSize);
    public Task<AdminStaffSummary> GetStaffSummaryAsync(CancellationToken cancellationToken = default)
    {
        var staff = Users.Where(user => user.UserRoles.Any(userRole => userRole.Role.Name is "Admin" or "SuperAdmin")).ToList();
        return Task.FromResult(new AdminStaffSummary(staff.Count, staff.Count(user => user.Status == UserStatus.Active), staff.Count(user => user.Status is UserStatus.Suspended or UserStatus.Deactivated), staff.Count(user => user.Status == UserStatus.Pending)));
    }
    public Task<User?> GetUserDetailsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Users.FirstOrDefault(user => user.Id == userId));
    public Task<(IReadOnlyList<User> Items, int TotalCount)> GetDriversAsync(string? search, UserStatus? status, KycStatus? kycStatus, VehicleDocumentReviewStatus? documentStatus, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(Users.Where(user => user.UserRoles.Any(userRole => userRole.Role.Name == "Driver") && (!status.HasValue || user.Status == status.Value) && (!kycStatus.HasValue || user.KycVerification?.Status == kycStatus.Value)), pageNumber, pageSize);
    public Task<AdminDriverTripSummary> GetDriverTripSummaryAsync(Guid driverId, CancellationToken cancellationToken = default) => Task.FromResult(new AdminDriverTripSummary(0, 0, 0));
    public Task<KycVerification?> GetKycDetailsAsync(Guid kycId, CancellationToken cancellationToken = default) => Task.FromResult(Kyc.FirstOrDefault(kyc => kyc.Id == kycId));
    public Task<Vehicle?> GetVehicleDetailsAsync(Guid vehicleId, CancellationToken cancellationToken = default) => Task.FromResult(Vehicles.FirstOrDefault(vehicle => vehicle.Id == vehicleId));
    public Task<AdminDashboardCounts> GetDashboardCountsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AdminDashboardCounts(Users.Count(user => !user.UserRoles.Any(role => role.Role.Name is "Admin" or "SuperAdmin")), Users.Count(user => user.UserRoles.Any(role => role.Role.Name == "Driver")), Users.Count(user => user.Status == UserStatus.Active && user.UserRoles.Any(role => role.Role.Name == "Driver")), Users.Count(user => user.Status == UserStatus.Pending && user.UserRoles.Any(role => role.Role.Name == "Driver")), Kyc.Count(kyc => kyc.Status is KycStatus.Pending or KycStatus.Submitted), VehicleDocuments.Count(document => document.ReviewStatus == VehicleDocumentReviewStatus.Pending), WalletTransactions.Count));
    public Task<IReadOnlyList<User>> GetRecentDriverRequestsAsync(int count, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>(Users.Where(user => user.UserRoles.Any(role => role.Role.Name == "Driver")).Take(count).ToList());
    public Task<(IReadOnlyList<WalletTransaction> Items, int TotalCount)> GetWalletTransactionsAsync(Guid? userId, WalletTransactionType? transactionType, string? status, DateTime? dateFrom, DateTime? dateTo, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(WalletTransactions.Where(transaction => (!userId.HasValue || transaction.Wallet.UserId == userId.Value) && (!transactionType.HasValue || transaction.Type == transactionType.Value)), pageNumber, pageSize);
    public Task<IReadOnlyList<WalletTransaction>> GetRecentWalletTransactionsAsync(int count, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WalletTransaction>>(WalletTransactions.Take(count).ToList());

    private static Task<(IReadOnlyList<T> Items, int TotalCount)> Page<T>(IEnumerable<T> source, int pageNumber, int pageSize)
    {
        var all = source.ToList();
        return Task.FromResult<(IReadOnlyList<T>, int)>((all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(), all.Count));
    }
}

internal sealed class TestEscrowRepository(TestTripBookingRepository bookings) : IEscrowRepository
{
    public List<Escrow> Items { get; } = [];
    public Task<Escrow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(escrow => escrow.Id == id));
    public Task<Escrow?> GetByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(escrow => escrow.BookingId == bookingId));
    public Task<IReadOnlyList<Escrow>> GetHeldByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Escrow>>(Items.Where(escrow => escrow.Status == EscrowStatus.Held && escrow.Booking.TripId == tripId).ToList());
    public Task<(IReadOnlyList<Escrow> Items, int TotalCount)> GetAsync(EscrowStatus? status, Guid? bookingId, Guid? passengerId, Guid? driverId, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Items.Where(escrow => (!status.HasValue || escrow.Status == status.Value) && (!bookingId.HasValue || escrow.BookingId == bookingId.Value) && (!passengerId.HasValue || escrow.PassengerId == passengerId.Value) && (!driverId.HasValue || escrow.DriverId == driverId.Value));
        var all = query.ToList();
        return Task.FromResult<(IReadOnlyList<Escrow>, int)>((all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(), all.Count));
    }
    public Task<EscrowTotals> GetTotalsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new EscrowTotals(Items.Where(escrow => escrow.Status == EscrowStatus.Held).Sum(escrow => escrow.Amount), Items.Where(escrow => escrow.Status == EscrowStatus.Released).Sum(escrow => escrow.Amount), Items.Where(escrow => escrow.Status == EscrowStatus.Refunded).Sum(escrow => escrow.Amount)));
    public Task<Escrow> CreateAsync(Escrow escrow, CancellationToken cancellationToken = default) { escrow.Booking = bookings.Items.Single(booking => booking.Id == escrow.BookingId); Items.Add(escrow); return Task.FromResult(escrow); }
    public void Update(Escrow escrow) { }
}

internal sealed class TestLedgerRepository : ILedgerRepository
{
    public List<LedgerAccount> Accounts { get; } = [];
    public List<LedgerTransaction> Transactions { get; } = [];
    public List<LedgerEntry> Entries { get; } = [];
    public Task<LedgerAccount?> GetAccountByCodeAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult(Accounts.FirstOrDefault(account => account.Code == code));
    public Task<LedgerTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult(Transactions.FirstOrDefault(transaction => transaction.IdempotencyKey == idempotencyKey));
    public Task<LedgerTransaction?> GetTransactionByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Transactions.FirstOrDefault(transaction => transaction.Id == id));
    public Task<(IReadOnlyList<LedgerTransaction> Items, int TotalCount)> GetTransactionsAsync(LedgerTransactionType? transactionType, LedgerTransactionStatus? status, string? reference, Guid? bookingId, Guid? escrowId, Guid? userId, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Transactions.Where(transaction => (!transactionType.HasValue || transaction.TransactionType == transactionType.Value) && (!status.HasValue || transaction.Status == status.Value) && (!bookingId.HasValue || transaction.BookingId == bookingId.Value) && (!escrowId.HasValue || transaction.EscrowId == escrowId.Value));
        var all = query.ToList();
        return Task.FromResult<(IReadOnlyList<LedgerTransaction>, int)>((all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(), all.Count));
    }
    public Task<LedgerFinancialTotals> GetFinancialTotalsAsync(CancellationToken cancellationToken = default)
    {
        var platform = Entries.Where(entry => entry.LedgerAccount.AccountType == LedgerAccountType.PlatformRevenue && entry.EntryType == LedgerEntryType.Credit).Sum(entry => entry.Amount);
        var payouts = Entries.Where(entry => entry.LedgerTransaction.TransactionType == LedgerTransactionType.EscrowRelease && entry.LedgerAccount.AccountType == LedgerAccountType.Wallet && entry.EntryType == LedgerEntryType.Credit).Sum(entry => entry.Amount);
        return Task.FromResult(new LedgerFinancialTotals(platform, platform, payouts, Transactions.Count));
    }
    public Task<IReadOnlyList<LedgerRevenueTotal>> GetRevenueSummaryAsync(DateTime dateFrom, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LedgerRevenueTotal>>(Entries.Where(entry => entry.CreatedAt >= dateFrom && entry.LedgerAccount.AccountType == LedgerAccountType.PlatformRevenue && entry.EntryType == LedgerEntryType.Credit).GroupBy(entry => entry.CreatedAt.Date).Select(group => new LedgerRevenueTotal(group.Key, group.Sum(entry => entry.Amount))).ToList());
    public Task<LedgerAccount> CreateAsync(LedgerAccount account, CancellationToken cancellationToken = default) { Accounts.Add(account); return Task.FromResult(account); }
    public Task<LedgerTransaction> CreateAsync(LedgerTransaction transaction, CancellationToken cancellationToken = default) { Transactions.Add(transaction); return Task.FromResult(transaction); }
    public Task<LedgerEntry> CreateAsync(LedgerEntry entry, CancellationToken cancellationToken = default) { Entries.Add(entry); entry.LedgerTransaction.Entries.Add(entry); return Task.FromResult(entry); }
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

internal sealed class TestUserRoleRepository(List<User> users, List<Role> roles) : IUserRoleRepository
{
    public List<UserRole> Items { get; } = [];
    public Task<IReadOnlyList<UserRole>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UserRole>>(Items.Where(r => r.UserId == userId).ToList());
    public Task<bool> ExistsAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Any(r => r.UserId == userId && r.RoleId == roleId));
    public Task<UserRole> CreateAsync(UserRole userRole, CancellationToken cancellationToken = default) { userRole.Role = roles.Single(role => role.Id == userRole.RoleId); userRole.User = users.Single(user => user.Id == userRole.UserId); userRole.User.UserRoles.Add(userRole); Items.Add(userRole); return Task.FromResult(userRole); }
    public void Delete(UserRole userRole) => Items.Remove(userRole);
}

internal sealed class TestProfileRepository(List<User> users) : IProfileRepository
{
    public List<Profile> Items { get; } = [];
    public Task<Profile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(p => p.Id == id));
    public Task<Profile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(p => p.UserId == userId));
    public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Any(p => p.UserId == userId));
    public Task<Profile> CreateAsync(Profile profile, CancellationToken cancellationToken = default) { var user = users.FirstOrDefault(user => user.Id == profile.UserId); if (user is not null) { profile.User = user; user.Profile = profile; } Items.Add(profile); return Task.FromResult(profile); }
    public void Update(Profile profile) { }
    public void Delete(Profile profile) => Items.Remove(profile);
}
