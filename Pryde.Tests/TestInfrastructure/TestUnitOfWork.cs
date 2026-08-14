using Pryde.Domain.Entities;
using Pryde.Domain.Common;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Tests.TestInfrastructure;

internal sealed class TestUnitOfWork : IUnitOfWork
{
    private readonly SemaphoreSlim _transactionLock = new(1, 1);

    public TestUnitOfWork()
    {
        AdminListings = new TestAdminListingRepository();
        Users = new TestUserRepository(((TestAdminListingRepository)AdminListings).Users);
        Roles = new TestRoleRepository();
        Trips = new TestTripRepository();
        RecurringTrips = new TestRecurringTripRepository();
        TripSubscriptions = new TestTripSubscriptionRepository(
            (TestRecurringTripRepository)RecurringTrips);
        TripBookings = new TestTripBookingRepository((TestTripRepository)Trips);
        Vehicles = new TestVehicleRepository();
        UserRoles = new TestUserRoleRepository(((TestUserRepository)Users).Items, ((TestRoleRepository)Roles).Items);
        Profiles = new TestProfileRepository(((TestUserRepository)Users).Items);
        VehicleDocuments = new TestVehicleDocumentRepository();
        VehicleImages = new TestVehicleImageRepository();
        VehicleAmenities = new TestVehicleAmenityRepository();
        Wallets = new TestWalletRepository();
        WalletTransactions = new TestWalletTransactionRepository(
            (TestWalletRepository)Wallets);
        PaystackWalletFundingRequests = new TestPaystackWalletFundingRequestRepository();
        KycVerifications = new TestKycVerificationRepository();
        KycVerificationAttempts = new TestKycVerificationAttemptRepository();
        PasswordResetCodes = new TestPasswordResetCodeRepository();
        VerificationCodes = new TestVerificationCodeRepository();
        RefreshTokens = new TestRefreshTokenRepository();
        Escrows = new TestEscrowRepository((TestTripBookingRepository)TripBookings);
        Ledger = new TestLedgerRepository();
        DriverBankAccounts = new TestDriverBankAccountRepository();
        Notifications = new TestNotificationRepository(
            ((TestUserRepository)Users).Items);
        TripRatings = new TestTripRatingRepository();
    }

    public TestTripRepository TripRepository => (TestTripRepository)Trips;
    public TestUserRepository UserRepository => (TestUserRepository)Users;
    public TestTripBookingRepository TripBookingRepository => (TestTripBookingRepository)TripBookings;
    public TestVehicleRepository VehicleRepository => (TestVehicleRepository)Vehicles;
    public TestVehicleDocumentRepository VehicleDocumentRepository =>
        (TestVehicleDocumentRepository)VehicleDocuments;
    public TestVehicleImageRepository VehicleImageRepository =>
        (TestVehicleImageRepository)VehicleImages;
    public TestVehicleAmenityRepository VehicleAmenityRepository =>
        (TestVehicleAmenityRepository)VehicleAmenities;
    public TestUserRoleRepository UserRoleRepository => (TestUserRoleRepository)UserRoles;
    public TestProfileRepository ProfileRepository => (TestProfileRepository)Profiles;
    public TestWalletRepository WalletRepository => (TestWalletRepository)Wallets;
    public TestWalletTransactionRepository WalletTransactionRepository => (TestWalletTransactionRepository)WalletTransactions;
    public TestPaystackWalletFundingRequestRepository PaystackWalletFundingRequestRepository =>
        (TestPaystackWalletFundingRequestRepository)PaystackWalletFundingRequests;
    public TestAdminListingRepository AdminListingRepository => (TestAdminListingRepository)AdminListings;
    public TestKycVerificationRepository KycVerificationRepository => (TestKycVerificationRepository)KycVerifications;
    public TestKycVerificationAttemptRepository KycVerificationAttemptRepository =>
        (TestKycVerificationAttemptRepository)KycVerificationAttempts;
    public TestEscrowRepository EscrowRepository => (TestEscrowRepository)Escrows;
    public TestLedgerRepository LedgerRepository => (TestLedgerRepository)Ledger;
    public TestVerificationCodeRepository VerificationCodeRepository =>
        (TestVerificationCodeRepository)VerificationCodes;
    public TestDriverBankAccountRepository DriverBankAccountRepository =>
        (TestDriverBankAccountRepository)DriverBankAccounts;
    public TestNotificationRepository NotificationRepository =>
        (TestNotificationRepository)Notifications;
    public TestTripRatingRepository TripRatingRepository =>
        (TestTripRatingRepository)TripRatings;
    public TestRecurringTripRepository RecurringTripRepository =>
        (TestRecurringTripRepository)RecurringTrips;
    public TestTripSubscriptionRepository TripSubscriptionRepository =>
        (TestTripSubscriptionRepository)TripSubscriptions;
    public int SaveChangesCount { get; private set; }
    public int TransactionCount { get; private set; }
    public Queue<int> SaveChangesResults { get; } = [];

    public IUserRepository Users { get; }
    public IRoleRepository Roles { get; }
    public IUserRoleRepository UserRoles { get; }
    public IProfileRepository Profiles { get; }
    public IKycVerificationRepository KycVerifications { get; }
    public IKycVerificationAttemptRepository KycVerificationAttempts { get; }
    public IVehicleRepository Vehicles { get; }
    public IVehicleDocumentRepository VehicleDocuments { get; }
    public IRefreshTokenRepository RefreshTokens { get; }
    public IPasswordResetCodeRepository PasswordResetCodes { get; }
    public IVerificationCodeRepository VerificationCodes { get; }
    public ITripRepository Trips { get; }
    public ITripBookingRepository TripBookings { get; }
    public IRecurringTripRepository RecurringTrips { get; }
    public ITripSubscriptionRepository TripSubscriptions { get; }
    public IWalletRepository Wallets { get; }
    public IWalletTransactionRepository WalletTransactions { get; }
    public IPaystackWalletFundingRequestRepository PaystackWalletFundingRequests { get; }
    public IVehicleImageRepository VehicleImages { get; }
    public IVehicleAmenityRepository VehicleAmenities { get; }
    public IAdminListingRepository AdminListings { get; }
    public IEscrowRepository Escrows { get; }
    public ILedgerRepository Ledger { get; }
    public IDriverBankAccountRepository DriverBankAccounts { get; }
    public INotificationRepository Notifications { get; }
    public ITripRatingRepository TripRatings { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        return Task.FromResult(
            SaveChangesResults.TryDequeue(out var result)
                ? result
                : 1);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        await _transactionLock.WaitAsync(cancellationToken);
        TransactionCount++;
        var users = UserRepository.Items.ToList();

        try
        {
            return await action(cancellationToken);
        }
        catch
        {
            UserRepository.Items.Clear();
            UserRepository.Items.AddRange(users);
            throw;
        }
        finally
        {
            _transactionLock.Release();
        }
    }

    public Task<T> ExecuteInTransactionOnceAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        return ExecuteInTransactionAsync(
            action,
            cancellationToken);
    }

    public void ClearTracking()
    {
    }
}

internal sealed class TestRecurringTripRepository
    : IRecurringTripRepository
{
    public List<RecurringTrip> Items { get; } = [];

    public Task<RecurringTrip?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

    public Task<RecurringTrip?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<RecurringTrip>> GetByDriverIdAsync(
        Guid driverId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RecurringTrip>>(Items
            .Where(item => item.DriverId == driverId)
            .OrderByDescending(item => item.CreatedAt)
            .ToList());

    public Task<IReadOnlyList<RecurringTrip>> GetActiveForGenerationAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RecurringTrip>>(Items
            .Where(item => item.IsActive && item.CancelledAt == null &&
                item.StartDate <= to &&
                (!item.EndDate.HasValue || item.EndDate.Value >= from))
            .ToList());

    public Task<(IReadOnlyList<RecurringTrip> Items, int TotalCount)> GetAllAsync(
        Guid? driverId,
        bool? isActive,
        bool? isCancelled,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = Items.AsEnumerable();
        if (driverId.HasValue)
            query = query.Where(item => item.DriverId == driverId.Value);
        if (isActive.HasValue)
            query = query.Where(item => item.IsActive == isActive.Value);
        if (isCancelled.HasValue)
            query = query.Where(item =>
                item.CancelledAt.HasValue == isCancelled.Value);
        var filtered = query.OrderByDescending(item => item.CreatedAt).ToList();
        return Task.FromResult((
            (IReadOnlyList<RecurringTrip>)filtered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList(),
            filtered.Count));
    }

    public Task<RecurringTrip> CreateAsync(
        RecurringTrip recurringTrip,
        CancellationToken cancellationToken = default)
    {
        Items.Add(recurringTrip);
        return Task.FromResult(recurringTrip);
    }

    public void Update(RecurringTrip recurringTrip)
    {
    }
}

internal sealed class TestTripSubscriptionRepository(
    TestRecurringTripRepository recurringTrips)
    : ITripSubscriptionRepository
{
    public List<TripSubscription> Items { get; } = [];

    public Task<TripSubscription?> GetByRecurringTripAndPassengerAsync(
        Guid recurringTripId,
        Guid passengerId,
        CancellationToken cancellationToken = default)
    {
        var subscription = Items.FirstOrDefault(item =>
            item.RecurringTripId == recurringTripId &&
            item.PassengerId == passengerId);
        if (subscription is not null && subscription.RecurringTrip is null)
            subscription.RecurringTrip = recurringTrips.Items
                .Single(item => item.Id == recurringTripId);
        return Task.FromResult(subscription);
    }

    public Task<IReadOnlyList<TripSubscription>> GetByPassengerIdAsync(
        Guid passengerId,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = Items
            .Where(item => item.PassengerId == passengerId)
            .OrderByDescending(item => item.CreatedAt)
            .ToList();
        foreach (var subscription in subscriptions)
            subscription.RecurringTrip = recurringTrips.Items
                .Single(item => item.Id == subscription.RecurringTripId);
        return Task.FromResult<IReadOnlyList<TripSubscription>>(subscriptions);
    }

    public Task<int> CountActiveAsync(
        Guid recurringTripId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Count(item =>
            item.RecurringTripId == recurringTripId && item.IsActive));

    public Task<TripSubscription> CreateAsync(
        TripSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        Items.Add(subscription);
        subscription.RecurringTrip.Subscriptions.Add(subscription);
        return Task.FromResult(subscription);
    }

    public void Update(TripSubscription subscription)
    {
    }
}

internal sealed class TestTripRatingRepository
    : ITripRatingRepository
{
    public List<TripRating> Items { get; } = [];
    public int RatingStateQueryCount { get; private set; }

    public Task<bool> ExistsAsync(
        Guid bookingId,
        Guid raterId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Items.Any(rating =>
            rating.BookingId == bookingId &&
            rating.RaterId == raterId));
    }

    public Task<IReadOnlyDictionary<Guid, DateTime>>
        GetCreatedAtByBookingIdsAndRaterAsync(
            IReadOnlyCollection<Guid> bookingIds,
            Guid raterId,
            CancellationToken cancellationToken = default)
    {
        RatingStateQueryCount++;
        IReadOnlyDictionary<Guid, DateTime> result = Items
            .Where(rating =>
                bookingIds.Contains(rating.BookingId) &&
                rating.RaterId == raterId)
            .ToDictionary(
                rating => rating.BookingId,
                rating => rating.CreatedAt);
        return Task.FromResult(result);
    }

    public Task<RatingSummaryData> GetSummaryAsync(
        Guid ratedUserId,
        CancellationToken cancellationToken = default)
    {
        var ratings = Items
            .Where(rating => rating.RatedUserId == ratedUserId)
            .ToList();
        return Task.FromResult(new RatingSummaryData(
            ratings.Count == 0
                ? 0
                : ratings.Average(rating => rating.Value),
            ratings.Count));
    }

    public Task<(
        IReadOnlyList<AdminTripRatingData> Items,
        int TotalCount)> GetAdminByRatedUserIdAsync(
            Guid ratedUserId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        var ratings = Items
            .Where(rating =>
                rating.RatedUserId == ratedUserId)
            .OrderByDescending(rating => rating.CreatedAt)
            .ThenByDescending(rating => rating.Id)
            .ToList();
        var items = ratings
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(rating => new AdminTripRatingData(
                rating.Id,
                rating.BookingId,
                rating.Booking.TripId,
                rating.Value,
                rating.Comment,
                rating.RaterId,
                rating.Rater.Profile is null
                    ? string.Empty
                    : $"{rating.Rater.Profile.FirstName} {rating.Rater.Profile.LastName}".Trim(),
                rating.RaterId ==
                    rating.Booking.Trip.DriverId
                        ? RoleType.Driver.ToString()
                        : RoleType.Passenger.ToString(),
                rating.RatedUserId,
                rating.Booking.Trip.OriginAddress,
                rating.Booking.Trip.DestinationAddress,
                rating.CreatedAt))
            .ToList();

        return Task.FromResult<(
            IReadOnlyList<AdminTripRatingData> Items,
            int TotalCount)>((items, ratings.Count));
    }

    public Task<TripRating> CreateAsync(
        TripRating rating,
        CancellationToken cancellationToken = default)
    {
        Items.Add(rating);
        return Task.FromResult(rating);
    }
}

internal sealed class TestNotificationRepository(
    List<User> users) : INotificationRepository
{
    public List<Notification> Items { get; } = [];
    public Exception? AddException { get; set; }

    public Task<Notification> AddAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        if (AddException is not null)
        {
            throw AddException;
        }

        Items.Add(notification);
        return Task.FromResult(notification);
    }

    public Task<Notification?> GetByIdAndUserIdAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Items.FirstOrDefault(
            notification =>
                notification.Id == notificationId &&
                notification.UserId == userId));
    }

    public Task<Notification?> GetByDeduplicationKeyAsync(
        string deduplicationKey,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Items.FirstOrDefault(
            notification =>
                notification.DeduplicationKey ==
                deduplicationKey));
    }

    public Task<(
        IReadOnlyList<Notification> Items,
        int TotalCount)> GetUserNotificationsAsync(
            Guid userId,
            int pageNumber,
            int pageSize,
            bool? isRead,
            NotificationType? type,
            CancellationToken cancellationToken = default)
    {
        var query = Items.Where(notification =>
            notification.UserId == userId);
        if (isRead.HasValue)
        {
            query = query.Where(notification =>
                notification.IsRead == isRead.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(notification =>
                notification.Type == type.Value);
        }

        var materialized = query
            .OrderByDescending(notification =>
                notification.CreatedAt)
            .ThenByDescending(notification =>
                notification.Id)
            .ToList();
        return Task.FromResult((
            (IReadOnlyList<Notification>)materialized
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList(),
            materialized.Count));
    }

    public Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Items.Count(notification =>
            notification.UserId == userId &&
            !notification.IsRead));
    }

    public Task<int> MarkAllAsReadAsync(
        Guid userId,
        DateTime readAt,
        CancellationToken cancellationToken = default)
    {
        var unread = Items.Where(notification =>
            notification.UserId == userId &&
            !notification.IsRead).ToList();
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = readAt;
            notification.UpdatedAt = readAt;
        }

        return Task.FromResult(unread.Count);
    }

    public Task<bool> ExistsByDeduplicationKeyAsync(
        string deduplicationKey,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Items.Any(notification =>
            notification.DeduplicationKey ==
            deduplicationKey));
    }

    public Task<(
        IReadOnlyList<AdminNotificationRecord> Items,
        int TotalCount)> AdminGetAllAsync(
            int pageNumber,
            int pageSize,
            Guid? userId,
            NotificationType? type,
            bool? isRead,
            DateTime? createdFrom,
            DateTime? createdTo,
            CancellationToken cancellationToken = default)
    {
        var query = Records();
        if (userId.HasValue)
        {
            query = query.Where(notification =>
                notification.UserId == userId.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(notification =>
                notification.Type == type.Value);
        }

        if (isRead.HasValue)
        {
            query = query.Where(notification =>
                notification.IsRead == isRead.Value);
        }

        if (createdFrom.HasValue)
        {
            query = query.Where(notification =>
                notification.CreatedAt >=
                createdFrom.Value.ToUniversalTime());
        }

        if (createdTo.HasValue)
        {
            query = query.Where(notification =>
                notification.CreatedAt <=
                createdTo.Value.ToUniversalTime());
        }

        var materialized = query
            .OrderByDescending(notification =>
                notification.CreatedAt)
            .ThenByDescending(notification =>
                notification.Id)
            .ToList();
        return Task.FromResult((
            (IReadOnlyList<AdminNotificationRecord>)
                materialized
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList(),
            materialized.Count));
    }

    public Task<AdminNotificationRecord?> AdminGetByIdAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Records().FirstOrDefault(
            notification =>
                notification.Id == notificationId));
    }

    public void Detach(Notification notification)
    {
        Items.Remove(notification);
    }

    private IEnumerable<AdminNotificationRecord> Records()
    {
        return Items.Select(notification =>
        {
            var user = users.FirstOrDefault(item =>
                item.Id == notification.UserId);
            var recipientName = user?.Profile is null
                ? string.Empty
                : $"{user.Profile.FirstName} {user.Profile.LastName}".Trim();
            return new AdminNotificationRecord(
                notification.Id,
                notification.UserId,
                recipientName,
                user?.Email ?? string.Empty,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.IsRead,
                notification.ReadAt,
                notification.RelatedEntityId,
                notification.RelatedEntityType,
                notification.Action,
                notification.CreatedAt);
        });
    }
}

internal sealed class TestDriverBankAccountRepository
    : IDriverBankAccountRepository
{
    public List<DriverBankAccount> Items { get; } = [];

    public Task<DriverBankAccount?> GetActiveByIdAndUserIdAsync(
        Guid bankAccountId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var bankAccount = Items.FirstOrDefault(item =>
            item.Id == bankAccountId &&
            item.UserId == userId &&
            item.IsActive);

        return Task.FromResult(bankAccount);
    }

    public Task<IReadOnlyList<DriverBankAccount>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var bankAccounts = Items
            .Where(bankAccount =>
                bankAccount.UserId == userId &&
                bankAccount.IsActive)
            .OrderByDescending(bankAccount => bankAccount.IsDefault)
            .ThenByDescending(bankAccount => bankAccount.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<DriverBankAccount>>(
            bankAccounts);
    }

    public Task<IReadOnlyList<DriverBankAccount>>
        GetActiveByUserIdForUpdateAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<DriverBankAccount>>(
            Items.Where(bankAccount =>
                    bankAccount.UserId == userId &&
                    bankAccount.IsActive)
                .ToList());
    }

    public Task<bool> ExistsAsync(
        Guid userId,
        string bankCode,
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        var exists = Items.Any(bankAccount =>
            bankAccount.UserId == userId &&
            bankAccount.BankCode == bankCode &&
            bankAccount.AccountNumber == accountNumber);

        return Task.FromResult(exists);
    }

    public Task<bool> HasAnyActiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var exists = Items.Any(bankAccount =>
            bankAccount.UserId == userId &&
            bankAccount.IsActive);

        return Task.FromResult(exists);
    }

    public Task<DriverBankAccount> CreateAsync(
        DriverBankAccount bankAccount,
        CancellationToken cancellationToken = default)
    {
        Items.Add(bankAccount);
        return Task.FromResult(bankAccount);
    }

    public void Update(DriverBankAccount bankAccount)
    {
    }
}

internal sealed class TestKycVerificationRepository : IKycVerificationRepository
{
    public List<KycVerification> Items { get; } = [];

    public Task<KycVerification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(kyc => kyc.Id == id));
    public Task<KycVerification?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(kyc => kyc.UserId == userId));
    public Task<KycVerification?> GetByUserIdForUpdateAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(kyc => kyc.UserId == userId));
    public Task<KycVerification?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(kyc => kyc.Id == id));
    public Task<KycVerification?> GetByProviderReferenceAsync(string providerReference, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(kyc => kyc.ProviderReference == providerReference));
    public Task<KycVerification?> GetByDojahReferenceAsync(string dojahReference, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(kyc => kyc.DojahReference == dojahReference));
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
    public HashSet<Guid> ProtectedDeletionUserIds { get; } = [];
    public List<Guid> DeletedWithRelatedDataUserIds { get; } = [];
    public Exception? DeleteWithRelatedDataException { get; set; }
    public Task<bool> ExistsByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(user => user.Id == userId));
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(user => user.Id == id));
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(user => user.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
    public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(user => user.PhoneNumber == phoneNumber));
    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>(Items.ToList());
    public Task<IReadOnlyList<Guid>> GetActiveNotificationRecipientIdsAsync(string? role, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(Items
            .Where(user => user.Status != UserStatus.Suspended &&
                           user.Status != UserStatus.Deactivated &&
                           !user.UserRoles.Any(userRole => userRole.Role.Name is "Admin" or "SuperAdmin") &&
                           user.UserRoles.Any(userRole => userRole.Role.Name is "Driver" or "Passenger") &&
                           (string.IsNullOrWhiteSpace(role) || user.UserRoles.Any(userRole => userRole.Role.Name == role)))
            .Select(user => user.Id)
            .ToList());
    public Task<bool> ExistsAsync(string email, string? phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult(Items.Any(user => user.Email.Equals(email, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrWhiteSpace(phoneNumber) && user.PhoneNumber == phoneNumber)));
    public Task<bool> HasProtectedDeletionRecordsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(ProtectedDeletionUserIds.Contains(userId));
    public Task DeleteWithRelatedDataAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        DeletedWithRelatedDataUserIds.Add(userId);
        Items.RemoveAll(user => user.Id == userId);
        if (DeleteWithRelatedDataException is not null)
        {
            throw DeleteWithRelatedDataException;
        }
        return Task.CompletedTask;
    }
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

    public Task<VerificationCode?> GetLatestActiveAsync(
        Guid userId, VerificationCodePurpose purpose, VerificationChannel channel,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(code => code.UserId == userId &&
                                           code.Purpose == purpose &&
                                           code.Channel == channel &&
                                           code.ConsumedAt == null &&
                                           code.ExpiresAt > DateTime.UtcNow)
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
    public Task<decimal?> GetBalanceByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Items
            .Where(wallet => wallet.UserId == userId)
            .Select(wallet => (decimal?)wallet.Balance)
            .FirstOrDefault());
    }
    public Task<Wallet> CreateAsync(Wallet wallet, CancellationToken cancellationToken = default) { Items.Add(wallet); return Task.FromResult(wallet); }
    public void Update(Wallet wallet) { }
}

internal sealed class TestWalletTransactionRepository(
    TestWalletRepository wallets) : IWalletTransactionRepository
{
    public List<WalletTransaction> Items { get; } = [];
    public Task<WalletTransaction> CreateAsync(WalletTransaction transaction, CancellationToken cancellationToken = default) { Items.Add(transaction); return Task.FromResult(transaction); }
    public Task<WalletTransaction?> GetByProviderReferenceAsync(string provider, string reference, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(transaction => transaction.Provider == provider && transaction.Reference == reference));
    public Task<WalletTransaction?> GetWithdrawalByProviderReferenceForUpdateAsync(string reference, CancellationToken cancellationToken = default)
    {
        var transaction = Items.FirstOrDefault(item =>
            item.Type == WalletTransactionType.Withdrawal &&
            item.Provider == "Paystack" &&
            item.Reference == reference);
        if (transaction is not null && transaction.Wallet is null)
        {
            transaction.Wallet = wallets.Items.Single(wallet => wallet.Id == transaction.WalletId);
        }
        return Task.FromResult(transaction);
    }
    public int PagedQueryCount { get; private set; }
    public int SumQueryCount { get; private set; }

    public Task<(
        IReadOnlyList<WalletTransaction> Items,
        int TotalCount)> GetByWalletIdAsync(
            Guid walletId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        PagedQueryCount++;
        var transactions = Items
            .Where(transaction => transaction.WalletId == walletId)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ToList();
        return Task.FromResult((
            (IReadOnlyList<WalletTransaction>)transactions
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList(),
            transactions.Count));
    }

    public Task<decimal> SumByUserIdAndTypeAsync(
        Guid userId,
        WalletTransactionType transactionType,
        DateTime? createdFrom,
        DateTime? createdTo,
        CancellationToken cancellationToken = default)
    {
        SumQueryCount++;
        var walletIds = wallets.Items
            .Where(wallet => wallet.UserId == userId)
            .Select(wallet => wallet.Id)
            .ToHashSet();
        var query = Items.Where(transaction =>
            walletIds.Contains(transaction.WalletId) &&
            transaction.Type == transactionType);

        if (createdFrom.HasValue)
        {
            query = query.Where(transaction =>
                transaction.CreatedAt >= createdFrom.Value);
        }

        if (createdTo.HasValue)
        {
            query = query.Where(transaction =>
                transaction.CreatedAt <= createdTo.Value);
        }

        return Task.FromResult(query.Sum(transaction => transaction.Amount));
    }

    public Task<IReadOnlyList<WalletTransaction>> GetWithdrawalsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var walletIds = wallets.Items
            .Where(wallet => wallet.UserId == userId)
            .Select(wallet => wallet.Id)
            .ToHashSet();
        var withdrawals = Items
            .Where(transaction =>
                walletIds.Contains(transaction.WalletId) &&
                transaction.Type == WalletTransactionType.Withdrawal)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<WalletTransaction>>(
            withdrawals);
    }

    public Task<WalletTransaction?> GetWithdrawalByIdAndUserIdAsync(
        Guid withdrawalId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var walletIds = wallets.Items
            .Where(wallet => wallet.UserId == userId)
            .Select(wallet => wallet.Id)
            .ToHashSet();
        var withdrawal = Items.FirstOrDefault(transaction =>
            transaction.Id == withdrawalId &&
            walletIds.Contains(transaction.WalletId) &&
            transaction.Type == WalletTransactionType.Withdrawal);

        return Task.FromResult(withdrawal);
    }
}

internal sealed class TestPaystackWalletFundingRequestRepository
    : IPaystackWalletFundingRequestRepository
{
    public List<PaystackWalletFundingRequest> Items { get; } = new();

    public Task<PaystackWalletFundingRequest> CreateAsync(
        PaystackWalletFundingRequest fundingRequest,
        CancellationToken cancellationToken = default)
    {
        Items.Add(fundingRequest);
        return Task.FromResult(fundingRequest);
    }

    public Task<PaystackWalletFundingRequest?> GetByReferenceAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Items.FirstOrDefault(
            fundingRequest => fundingRequest.Reference == reference));
    }

    public Task<PaystackWalletFundingRequest?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Items.FirstOrDefault(
            fundingRequest => fundingRequest.Id == id));
    }

    public Task<PaystackWalletFundingRequest?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(id, cancellationToken);
    }
}

internal sealed class TestKycVerificationAttemptRepository
    : IKycVerificationAttemptRepository
{
    public List<KycVerificationAttempt> Items { get; } = [];

    public Task<KycVerificationAttempt?> GetByCorrelationReferenceAsync(
        string providerName,
        string correlationReference,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(x =>
            x.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase) &&
            x.CorrelationReference.Equals(correlationReference, StringComparison.OrdinalIgnoreCase)));

    public Task<KycVerificationAttempt?> GetByProviderReferenceAsync(
        string providerName,
        string providerReference,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(x =>
            x.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase) &&
            x.ProviderReference != null &&
            x.ProviderReference.Equals(providerReference, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<KycVerificationAttempt>> GetByKycVerificationIdAsync(
        Guid kycVerificationId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<KycVerificationAttempt>>(
            Items.Where(x => x.KycVerificationId == kycVerificationId)
                .OrderBy(x => x.StartedAt)
                .ToList());

    public Task<KycVerificationAttempt> CreateAsync(
        KycVerificationAttempt attempt,
        CancellationToken cancellationToken = default)
    {
        Items.Add(attempt);
        return Task.FromResult(attempt);
    }

    public void Update(KycVerificationAttempt attempt)
    {
    }
}

internal sealed class TestAdminListingRepository : IAdminListingRepository
{
    public List<User> Users { get; } = [];
    public List<KycVerification> Kyc { get; } = [];
    public List<Vehicle> Vehicles { get; } = [];
    public List<VehicleDocument> VehicleDocuments { get; } = [];
    public List<Trip> Trips { get; } = [];
    public List<TripBooking> Bookings { get; } = [];
    public List<WalletTransaction> WalletTransactions { get; } = [];

    public Task<(IReadOnlyList<User> Items, int TotalCount)> GetUsersAsync(string? role, UserStatus? status, string? search, bool? isActive, bool? isEmailVerified, bool? isPhoneVerified, KycStatus? kycStatus, DateTime? createdFrom, DateTime? createdTo, string? sortBy, string? sortDirection, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Users.Where(user =>
            (!status.HasValue || user.Status == status.Value) &&
            (!isActive.HasValue || (user.Status == UserStatus.Active) == isActive.Value) &&
            (!isEmailVerified.HasValue || user.IsEmailVerified == isEmailVerified.Value) &&
            (!isPhoneVerified.HasValue || user.IsPhoneNumberVerified == isPhoneVerified.Value) &&
            (!kycStatus.HasValue || user.KycVerification?.Status == kycStatus.Value) &&
            (!createdFrom.HasValue || user.CreatedAt >= createdFrom.Value) &&
            (!createdTo.HasValue || user.CreatedAt <= createdTo.Value) &&
            (string.IsNullOrWhiteSpace(role) || user.UserRoles.Any(userRole =>
                userRole.Role.Name.Equals(role, StringComparison.OrdinalIgnoreCase))) &&
            (string.IsNullOrWhiteSpace(search) ||
             user.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             user.PhoneNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             (user.Profile?.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
             (user.Profile?.LastName.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)));
        return Page(query, pageNumber, pageSize);
    }
    public Task<(IReadOnlyList<KycVerification> Items, int TotalCount)> GetKycAsync(KycStatus? status, string? role, string? provider, string? search, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(Kyc.Where(kyc => (!status.HasValue || kyc.Status == status.Value) && (string.IsNullOrWhiteSpace(provider) || kyc.ProviderName == provider)), pageNumber, pageSize);
    public Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> GetVehiclesAsync(VehicleOnboardingStatus? onboardingStatus, bool? isActive, Guid? ownerId, VehicleRegistrationType? registrationType, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(Vehicles.Where(vehicle => (!onboardingStatus.HasValue || vehicle.OnboardingStatus == onboardingStatus.Value) && (!isActive.HasValue || vehicle.IsActive == isActive.Value) && (!ownerId.HasValue || vehicle.UserId == ownerId.Value) && (!registrationType.HasValue || vehicle.RegistrationType == registrationType.Value)), pageNumber, pageSize);
    public Task<(IReadOnlyList<VehicleDocument> Items, int TotalCount)> GetVehicleDocumentsAsync(Guid? vehicleId, Guid? ownerId, VehicleDocumentType? documentType, VehicleDocumentReviewStatus? reviewStatus, DateTime? expiryFrom, DateTime? expiryTo, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(VehicleDocuments.Where(document => (!vehicleId.HasValue || document.VehicleId == vehicleId.Value) && (!ownerId.HasValue || document.Vehicle.UserId == ownerId.Value) && (!documentType.HasValue || document.DocumentType == documentType.Value) && (!reviewStatus.HasValue || document.ReviewStatus == reviewStatus.Value) && (!expiryFrom.HasValue || document.ExpiryDate >= expiryFrom.Value) && (!expiryTo.HasValue || document.ExpiryDate <= expiryTo.Value)), pageNumber, pageSize);
    public Task<(IReadOnlyList<Trip> Items, int TotalCount)> GetTripsAsync(string? search, Guid? driverId, TripStatus? status, DateTime? departureFrom, DateTime? departureTo, bool? isRecurring, bool? isActive, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(Trips.Where(trip => (!driverId.HasValue || trip.DriverId == driverId.Value) && (!status.HasValue || trip.Status == status.Value) && (!departureFrom.HasValue || trip.DepartureTime >= departureFrom.Value) && (!departureTo.HasValue || trip.DepartureTime <= departureTo.Value) && (!isRecurring.HasValue || trip.RecurringTripId.HasValue == isRecurring.Value) && (!isActive.HasValue || (trip.Status is not (TripStatus.Completed or TripStatus.Cancelled)) == isActive.Value)), pageNumber, pageSize);
    public Task<Trip?> GetTripAsync(Guid tripId, CancellationToken cancellationToken = default) => Task.FromResult(Trips.FirstOrDefault(trip => trip.Id == tripId));
    public Task<(IReadOnlyList<TripBooking> Items, int TotalCount)> GetBookingsAsync(Guid? userId, Guid? driverId, Guid? tripId, BookingStatus? status, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(Bookings.Where(booking => (!userId.HasValue || booking.PassengerId == userId.Value) && (!driverId.HasValue || booking.Trip.DriverId == driverId.Value) && (!tripId.HasValue || booking.TripId == tripId.Value) && (!status.HasValue || booking.Status == status.Value) && (!dateFrom.HasValue || booking.RequestedAt >= dateFrom.Value) && (!dateTo.HasValue || booking.RequestedAt <= dateTo.Value)), pageNumber, pageSize);
    public Task<TripBooking?> GetBookingAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult(Bookings.FirstOrDefault(booking => booking.Id == bookingId));
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
    public Task<(IReadOnlyList<WalletTransaction> Items, int TotalCount)> GetWalletTransactionsAsync(Guid? userId, WalletTransactionType? transactionType, string? status, DateTime? dateFrom, DateTime? dateTo, string? reference, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Page(WalletTransactions.Where(transaction => (!userId.HasValue || transaction.Wallet.UserId == userId.Value) && (!transactionType.HasValue || transaction.Type == transactionType.Value) && (string.IsNullOrWhiteSpace(reference) || (transaction.Reference?.Contains(reference, StringComparison.OrdinalIgnoreCase) ?? false))), pageNumber, pageSize);
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
    public int GetAllByDriverQueryCount { get; private set; }
    public int DashboardQueryCount { get; private set; }

    public Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(t => t.Id == id));

    public Task<Trip?> GetByIdWithBookingsAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<Trip?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<Trip?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<Trip?> GetByIdWithVehicleForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Trip>> GetByDriverIdAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        GetAllByDriverQueryCount++;
        return Task.FromResult<IReadOnlyList<Trip>>(
            Items.Where(trip => trip.DriverId == driverId).ToList());
    }

    public Task<int> CountCompletedByDriverIdAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        DashboardQueryCount++;
        return Task.FromResult(Items.Count(trip =>
            trip.DriverId == driverId &&
            trip.Status == TripStatus.Completed));
    }

    public Task<DriverDashboardTripSummaryData?> GetNextUpcomingByDriverIdAsync(
        Guid driverId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        DashboardQueryCount++;
        var trip = Items
            .Where(trip =>
                trip.DriverId == driverId &&
                trip.DepartureTime > utcNow &&
                trip.Status != TripStatus.Completed &&
                trip.Status != TripStatus.Cancelled)
            .OrderBy(trip => trip.DepartureTime)
            .FirstOrDefault();
        return Task.FromResult(
            trip is null ? null : DashboardSummary(trip));
    }

    public Task<IReadOnlyList<DriverDashboardTripSummaryData>>
        GetLatestCompletedByDriverIdAsync(
        Guid driverId,
        int count,
        CancellationToken cancellationToken = default)
    {
        DashboardQueryCount++;
        return Task.FromResult<IReadOnlyList<DriverDashboardTripSummaryData>>(
            Items
            .Where(trip =>
                trip.DriverId == driverId &&
                trip.Status == TripStatus.Completed)
            .OrderByDescending(trip => trip.DepartureTime)
            .Take(count)
            .Select(DashboardSummary)
            .ToList());
    }

    private static DriverDashboardTripSummaryData DashboardSummary(
        Trip trip)
    {
        var imageUrl = trip.Vehicle.Images
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image =>
                image.ImageType == VehicleImageType.FrontView ? 0 : 1)
            .ThenBy(image => image.ImageType)
            .ThenBy(image => image.Id)
            .Select(image => image.ImageUrl)
            .FirstOrDefault();

        return new DriverDashboardTripSummaryData
        {
            TripId = trip.Id,
            OriginAddress = trip.OriginAddress,
            DestinationAddress = trip.DestinationAddress,
            DepartureTime = trip.DepartureTime,
            Status = trip.Status,
            SeatPrice = trip.SeatPrice,
            AvailableSeats = trip.AvailableSeats,
            VehicleLicensePlateNumber = trip.Vehicle.LicensePlateNumber,
            VehicleImageUrl = imageUrl
        };
    }

    public Task<IReadOnlyList<Trip>> SearchAsync(
        DateTime utcNow, DateTime? departureDate, bool? requiresLuggage,
        int requiredSeats, double? pickupLatitude,
        double? pickupLongitude, double? pickupRadiusKm,
        CancellationToken cancellationToken = default)
    {
        var isBookingOpen = TripBookingWindow
            .IsOpenAtUtc(utcNow)
            .Compile();
        var query = Items.Where(t => t.Status == TripStatus.Scheduled
            && t.DepartureTime > utcNow
            && isBookingOpen(t)
            && t.AvailableSeats >= requiredSeats);
        if (departureDate.HasValue)
            query = query.Where(t => t.DepartureTime.Date == departureDate.Value.Date);
        if (requiresLuggage == true)
            query = query.Where(t => t.AllowLuggage);
        if (pickupLatitude.HasValue &&
            pickupLongitude.HasValue &&
            pickupRadiusKm.HasValue)
        {
            query = query.Where(trip => PickupDistanceKm(
                    pickupLatitude.Value,
                    pickupLongitude.Value,
                    trip.OriginLatitude,
                    trip.OriginLongitude) <= pickupRadiusKm.Value);
        }
        return Task.FromResult<IReadOnlyList<Trip>>(query.OrderBy(t => t.DepartureTime).ToList());
    }

    private static double PickupDistanceKm(
        double latitude,
        double longitude,
        double pickupLatitude,
        double pickupLongitude)
    {
        const double earthRadiusKm = 6371d;
        const double degreesToRadians = Math.PI / 180d;
        var latitudeDelta =
            (pickupLatitude - latitude) * degreesToRadians;
        var longitudeDelta =
            (pickupLongitude - longitude) * degreesToRadians;
        var haversine =
            Math.Pow(Math.Sin(latitudeDelta / 2d), 2d) +
            Math.Cos(latitude * degreesToRadians) *
            Math.Cos(pickupLatitude * degreesToRadians) *
            Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);
        return 2d * earthRadiusKm *
            Math.Asin(Math.Sqrt(haversine));
    }

    public Task<IReadOnlyList<Trip>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Trip>>(Items.Where(t => t.Status == TripStatus.Scheduled && t.AvailableSeats > 0).ToList());

    public Task<bool> RecurringOccurrenceExistsAsync(
        Guid recurringTripId,
        DateTime departureTime,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(trip =>
            trip.RecurringTripId == recurringTripId &&
            trip.DepartureTime == departureTime));

    public Task<IReadOnlyList<Guid>>
        GetExpiredConfirmationTripIdsAsync(
            DateTime utcNow,
            CancellationToken cancellationToken = default)
    {
        var tripIds = Items
            .Where(trip =>
                trip.Status ==
                    TripStatus.DropoffConfirmationPending &&
                trip.DriverEndedAt.HasValue &&
                trip.ConfirmationDeadline.HasValue &&
                trip.ConfirmationDeadline.Value <= utcNow)
            .OrderBy(trip => trip.ConfirmationDeadline)
            .Select(trip => trip.Id)
            .ToList();

        return Task.FromResult<IReadOnlyList<Guid>>(tripIds);
    }

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

    public Task<TripBooking?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<TripBooking?> GetByIdWithTripAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<TripBooking>> GetByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TripBooking>>(Items.Where(b => b.TripId == tripId).ToList());

    public Task<IReadOnlyList<TripBooking>> GetPendingByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TripBooking>>(Items.Where(b => b.TripId == tripId && b.Status == BookingStatus.Pending).ToList());

    public Task<(IReadOnlyList<DriverPendingBookingRequestData> Items,
        int TotalCount)>
        GetPendingByDriverIdAsync(
            Guid driverId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        var query = Items
            .Where(booking =>
                booking.Trip.DriverId == driverId &&
                booking.Status == BookingStatus.Pending)
            .OrderByDescending(booking => booking.RequestedAt);
        var totalCount = query.Count();
        var bookings = query
            .Select(booking => new DriverPendingBookingRequestData
            {
                BookingId = booking.Id,
                TripId = booking.TripId,
                PassengerId = booking.PassengerId,
                PassengerName = booking.Passenger.Profile is null
                    ? null
                    : $"{booking.Passenger.Profile.FirstName} " +
                      $"{booking.Passenger.Profile.LastName}".Trim(),
                PassengerProfileImageUrl =
                    booking.Passenger.Profile?.ProfilePhotoUrl,
                PickupLocation = booking.Trip.OriginAddress,
                Destination = booking.Trip.DestinationAddress,
                TripDepartureTime = booking.Trip.DepartureTime,
                RequestedAt = booking.RequestedAt
            })
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<
            (IReadOnlyList<DriverPendingBookingRequestData> Items,
                int TotalCount)>((bookings, totalCount));
    }

    public Task<IReadOnlyList<TripBooking>> GetApprovedByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TripBooking>>(Items.Where(b =>
            b.TripId == tripId &&
            (b.Status == BookingStatus.Approved ||
             b.Status == BookingStatus.Completed)).ToList());

    public Task<IReadOnlyList<TripBooking>> GetByPassengerIdAsync(Guid passengerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TripBooking>>(Items.Where(b => b.PassengerId == passengerId).ToList());

    public Task<int> CountPendingByDriverIdAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var pendingBookingCount = Items.Count(booking =>
            booking.Trip.DriverId == driverId &&
            booking.Status == BookingStatus.Pending);

        return Task.FromResult(pendingBookingCount);
    }

    public Task<IReadOnlyList<Guid>>
        GetExpiredUnpaidApprovedBookingIdsAsync(
            DateTime utcNow,
            CancellationToken cancellationToken = default)
    {
        var bookingIds = Items
            .Where(booking =>
                booking.Status == BookingStatus.Approved &&
                !booking.PaidAt.HasValue &&
                booking.PaymentExpiresAt.HasValue &&
                booking.PaymentExpiresAt.Value <= utcNow)
            .OrderBy(booking => booking.PaymentExpiresAt)
            .Select(booking => booking.Id)
            .ToList();
        return Task.FromResult<IReadOnlyList<Guid>>(
            bookingIds);
    }

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

internal sealed class TestVehicleImageRepository : IVehicleImageRepository
{
    public List<VehicleImage> Items { get; } = [];
    public Task<VehicleImage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
    public Task<IReadOnlyList<VehicleImage>> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VehicleImage>>(Items.Where(x => x.VehicleId == vehicleId).ToList());
    public Task<VehicleImage> CreateAsync(VehicleImage image, CancellationToken cancellationToken = default)
    {
        if (image.ImageType.HasValue &&
            Items.Any(x => x.VehicleId == image.VehicleId && x.ImageType == image.ImageType))
        {
            throw new InvalidOperationException("Duplicate typed vehicle image.");
        }
        Items.Add(image);
        return Task.FromResult(image);
    }
    public void Update(VehicleImage image) { }
    public void Delete(VehicleImage image) => Items.Remove(image);
}

internal sealed class TestVehicleAmenityRepository : IVehicleAmenityRepository
{
    public List<VehicleAmenity> Items { get; } = [];
    public Task<IReadOnlyList<VehicleAmenity>> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VehicleAmenity>>(Items.Where(x => x.VehicleId == vehicleId).ToList());
    public Task<VehicleAmenity> CreateAsync(VehicleAmenity amenity, CancellationToken cancellationToken = default)
    {
        if (Items.Any(x => x.VehicleId == amenity.VehicleId && x.AmenityType == amenity.AmenityType))
            throw new InvalidOperationException("Duplicate vehicle amenity.");
        Items.Add(amenity);
        return Task.FromResult(amenity);
    }
    public void Delete(VehicleAmenity amenity) => Items.Remove(amenity);
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
