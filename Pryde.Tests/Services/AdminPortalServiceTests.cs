using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Notifications.Interface;
using Pryde.Services.Providers.Dojah;
using Pryde.Services.Security.Implementation;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pryde.Tests.Services;

public class AdminPortalServiceTests
{
    [Fact]
    public async Task AdminReceivesLocalKycAndDojahDetails()
    {
        var unitOfWork = new TestUnitOfWork();
        var user = CreateKycUser();
        var kyc = new KycVerification
        {
            User = user,
            UserId = user.Id,
            Status = KycStatus.Approved,
            ProviderName = "Dojah",
            ProviderReference = "PRYDE-local-reference",
            DojahReference = "provider-generated-reference"
        };
        unitOfWork.AdminListingRepository.Kyc.Add(kyc);
        var dojahClient = new FakeDojahApiClient
        {
            Result = new DojahVerificationDetailsResponseDto
            {
                Reference = "provider-generated-reference",
                Status = "Completed"
            }
        };
        var service = CreateService(unitOfWork, new FakeEmailService(), dojahClient);

        var result = await service.GetKycAsync(kyc.Id);

        Assert.Equal(kyc.Id, result.Id);
        Assert.Equal("driver.kyc@test.local", result.Email);
        Assert.Equal("PRYDE-local-reference", result.ProviderReference);
        Assert.Equal("provider-generated-reference", result.DojahReference);
        Assert.NotNull(result.DojahDetails);
        Assert.Equal("Completed", result.DojahDetails.Status);
        Assert.Equal(
            "provider-generated-reference",
            dojahClient.RequestedReference);
        Assert.NotEqual(
            result.ProviderReference,
            dojahClient.RequestedReference);
        Assert.Equal(1, dojahClient.CallCount);
    }

    [Fact]
    public async Task NoDojahReferenceReturnsLocalKycWithNullDojahDetailsWithoutProviderCall()
    {
        var unitOfWork = new TestUnitOfWork();
        var user = CreateKycUser();
        var kyc = new KycVerification
        {
            User = user,
            UserId = user.Id,
            Status = KycStatus.Pending,
            ProviderReference = "pryde-reference"
        };
        unitOfWork.AdminListingRepository.Kyc.Add(kyc);
        var dojahClient = new FakeDojahApiClient();
        var service = CreateService(unitOfWork, new FakeEmailService(), dojahClient);

        var result = await service.GetKycAsync(kyc.Id);

        Assert.Equal(kyc.Id, result.Id);
        Assert.Equal("pryde-reference", result.ProviderReference);
        Assert.Null(result.DojahDetails);
        Assert.Equal(0, dojahClient.CallCount);
    }

    [Fact]
    public async Task ProviderFailureReturnsLocalKycWithNullDetailsWithoutModifyingState()
    {
        var unitOfWork = new TestUnitOfWork();
        var user = CreateKycUser();
        var kyc = new KycVerification
        {
            User = user,
            UserId = user.Id,
            Status = KycStatus.Approved,
            ProviderStatus = "Completed",
            ProviderReference = "PRYDE-local-reference",
            DojahReference = "provider-generated-reference"
        };
        unitOfWork.AdminListingRepository.Kyc.Add(kyc);
        var dojahClient = new FakeDojahApiClient
        {
            Exception = new ServiceUnavailableException(
                "Dojah is unavailable.")
        };
        var service = CreateService(
            unitOfWork,
            new FakeEmailService(),
            dojahClient);

        var result = await service.GetKycAsync(kyc.Id);

        Assert.Equal(kyc.Id, result.Id);
        Assert.Null(result.DojahDetails);
        Assert.Equal(KycStatus.Approved, kyc.Status);
        Assert.Equal("Completed", kyc.ProviderStatus);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task SuperAdminInvitationCreatesPendingStaffAndSendsExpiringCode()
    {
        var unitOfWork = new TestUnitOfWork();
        var email = new FakeEmailService();
        var service = CreateService(unitOfWork, email);

        var result = await service.InviteStaffAsync(new InviteStaffRequestDto
        {
            FirstName = "Ada",
            LastName = "Admin",
            Email = "ada.admin@test.local",
            Role = "Admin"
        });

        Assert.Equal("Pending", result.Status);
        Assert.Equal("Admin", result.Role);
        Assert.Single(email.Messages);
        Assert.True(((TestPasswordResetCodeRepository)unitOfWork.PasswordResetCodes).Items.Single().ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task DuplicateUnexpiredInvitationIsRejected()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = CreateService(unitOfWork, new FakeEmailService());
        var request = new InviteStaffRequestDto
        {
            FirstName = "Ada", LastName = "Admin", Email = "duplicate@test.local", Role = "Admin"
        };
        await service.InviteStaffAsync(request);

        await Assert.ThrowsAsync<ConflictException>(() => service.InviteStaffAsync(request));
    }

    [Fact]
    public async Task StaffListingIsPagedAndContainsSummary()
    {
        var unitOfWork = new TestUnitOfWork();
        AddStaff(unitOfWork, "Admin", UserStatus.Active);
        AddStaff(unitOfWork, "Admin", UserStatus.Deactivated);
        var service = CreateService(unitOfWork, new FakeEmailService());

        var result = await service.GetStaffAsync(new AdminStaffRequestDto { PageNumber = 1, PageSize = 1 });

        Assert.Single(result.Items);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Summary.ActiveStaff);
        Assert.Equal(1, result.Summary.InactiveStaff);
    }

    [Fact]
    public async Task SuperAdminCannotDeactivateSelfOrFinalActiveSuperAdmin()
    {
        var unitOfWork = new TestUnitOfWork();
        var target = AddStaff(unitOfWork, "SuperAdmin", UserStatus.Active);
        var service = CreateService(unitOfWork, new FakeEmailService());

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.DeactivateStaffAsync(target.Id, target.Id));
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.DeactivateStaffAsync(target.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task SuperAdminCanDeactivateAndReactivateAdmin()
    {
        var unitOfWork = new TestUnitOfWork();
        var admin = AddStaff(unitOfWork, "Admin", UserStatus.Active);
        var service = CreateService(unitOfWork, new FakeEmailService());

        var deactivated = await service.DeactivateStaffAsync(admin.Id, Guid.NewGuid());
        var activated = await service.ActivateStaffAsync(admin.Id);

        Assert.Equal("Deactivated", deactivated.Status);
        Assert.Equal("Active", activated.Status);
    }

    [Fact]
    public async Task DashboardUsesRepositoryAndLedgerAggregates()
    {
        var unitOfWork = new TestUnitOfWork();
        AddStaff(unitOfWork, "Admin", UserStatus.Active);
        var driver = new User
        {
            Email = "driver.dashboard@test.local",
            PhoneNumber = "1234567890",
            Status = UserStatus.Active,
            Profile = new Profile { FirstName = "Dashboard", LastName = "Driver" }
        };
        var driverRole = new Role { Name = "Driver" };
        driver.UserRoles.Add(new UserRole { User = driver, UserId = driver.Id, Role = driverRole, RoleId = driverRole.Id });
        var kyc = new KycVerification { User = driver, UserId = driver.Id, Status = KycStatus.Pending };
        driver.KycVerification = kyc;
        ((TestUserRepository)unitOfWork.Users).Items.Add(driver);
        unitOfWork.AdminListingRepository.Kyc.Add(kyc);
        var account = new LedgerAccount { AccountType = LedgerAccountType.PlatformRevenue };
        var transaction = new LedgerTransaction { TransactionType = LedgerTransactionType.EscrowRelease };
        var entry = new LedgerEntry
        {
            LedgerAccount = account,
            LedgerTransaction = transaction,
            EntryType = LedgerEntryType.Credit,
            Amount = 250m
        };
        transaction.Entries.Add(entry);
        unitOfWork.LedgerRepository.Accounts.Add(account);
        unitOfWork.LedgerRepository.Transactions.Add(transaction);
        unitOfWork.LedgerRepository.Entries.Add(entry);
        var service = CreateService(unitOfWork, new FakeEmailService());

        var result = await service.GetDashboardAsync();

        Assert.Equal(1, result.TotalUsers);
        Assert.Equal(1, result.TotalDrivers);
        Assert.Equal(1, result.ActiveDrivers);
        Assert.Equal(1, result.PendingKycRequests);
        Assert.Equal(1, result.TotalStaff);
        Assert.Equal(250m, result.TotalPlatformEarnings);
        Assert.Equal(1, result.TotalTransactions);
    }

    [Fact]
    public async Task DriverApprovalAndRejectionUseSeparateNotificationTypes()
    {
        var unitOfWork = new TestUnitOfWork();
        var driver = new User
        {
            Email = "driver-review@test.local",
            Status = UserStatus.Pending,
            Profile = new Profile
            {
                FirstName = "Review",
                LastName = "Driver"
            }
        };
        var role = new Role { Name = "Driver" };
        driver.UserRoles.Add(new UserRole
        {
            UserId = driver.Id,
            User = driver,
            RoleId = role.Id,
            Role = role
        });
        unitOfWork.UserRepository.Items.Add(driver);
        var service = CreateService(
            unitOfWork,
            new FakeEmailService());

        await service.ActivateDriverAsync(driver.Id);
        await service.DeactivateDriverAsync(driver.Id);

        Assert.Contains(
            unitOfWork.NotificationRepository.Items,
            notification =>
                notification.UserId == driver.Id &&
                notification.Type == NotificationType.DriverApproved);
        Assert.Contains(
            unitOfWork.NotificationRepository.Items,
            notification =>
                notification.UserId == driver.Id &&
                notification.Type == NotificationType.DriverRejected);
    }

    private static AdminPortalService CreateService(
        TestUnitOfWork unitOfWork,
        IEmailService email,
        IDojahApiClient? dojahApiClient = null) =>
        new(
            unitOfWork,
            new PasswordHasher(),
            email,
            new FinancialService(unitOfWork),
            dojahApiClient ?? new FakeDojahApiClient(),
            NullLogger<AdminPortalService>.Instance);

    private static User CreateKycUser()
    {
        var user = new User
        {
            Email = "driver.kyc@test.local",
            PhoneNumber = "08000000001",
            Profile = new Profile
            {
                FirstName = "Test",
                LastName = "Driver"
            }
        };
        var role = new Role { Name = "Driver" };
        user.UserRoles.Add(new UserRole
        {
            User = user,
            UserId = user.Id,
            Role = role,
            RoleId = role.Id
        });
        return user;
    }

    private static User AddStaff(TestUnitOfWork unitOfWork, string roleName, UserStatus status)
    {
        var user = new User
        {
            Email = $"{Guid.NewGuid():N}@test.local",
            PhoneNumber = Guid.NewGuid().ToString("N")[..20],
            Status = status,
            Profile = new Profile { FirstName = "Test", LastName = roleName }
        };
        var role = new Role { Name = roleName };
        user.UserRoles.Add(new UserRole { UserId = user.Id, User = user, RoleId = role.Id, Role = role });
        ((TestUserRepository)unitOfWork.Users).Items.Add(user);
        return user;
    }

    private sealed class FakeEmailService : IEmailService
    {
        public List<(string Email, string Subject)> Messages { get; } = [];
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            Messages.Add((toEmail, subject));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDojahApiClient : IDojahApiClient
    {
        public DojahVerificationDetailsResponseDto Result { get; set; } = new()
        {
            Reference = "unused"
        };
        public int CallCount { get; private set; }
        public string? RequestedReference { get; private set; }
        public Exception? Exception { get; set; }

        public Task<DojahVerificationDetailsResponseDto> GetVerificationAsync(
            string dojahReference,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            RequestedReference = dojahReference;
            if (Exception is not null)
            {
                return Task.FromException<DojahVerificationDetailsResponseDto>(
                    Exception);
            }

            return Task.FromResult(Result);
        }
    }
}
