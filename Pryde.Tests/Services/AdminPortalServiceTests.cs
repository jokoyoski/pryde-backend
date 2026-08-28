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
                Status = "Completed",
                FirstName = "Verified",
                LastName = "Person",
                Gender = "Female",
                SelfieImageUrl = "https://media.dojah.io/selfie.jpg"
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
        Assert.Equal("Verified", result.DojahDetails.FirstName);
        Assert.Equal("Person", result.DojahDetails.LastName);
        Assert.Equal("Female", result.DojahDetails.Gender);
        Assert.Equal(
            "https://media.dojah.io/selfie.jpg",
            result.DojahDetails.SelfieImageUrl);
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
    public async Task ProviderNotFoundReturnsSelectedLocalKycWithNullDetails()
    {
        var unitOfWork = new TestUnitOfWork();
        var user = CreateKycUser();
        var kyc = new KycVerification
        {
            User = user,
            UserId = user.Id,
            Status = KycStatus.Rejected,
            ProviderReference = "PRYDE-selected-reference",
            DojahReference = "provider-missing-reference",
            ProviderStatus = "Failed",
            RejectionReason = "Identity checks failed."
        };
        unitOfWork.AdminListingRepository.Kyc.Add(kyc);
        var dojahClient = new FakeDojahApiClient
        {
            Exception = new NotFoundException(
                "Dojah verification",
                kyc.DojahReference)
        };
        var service = CreateService(
            unitOfWork,
            new FakeEmailService(),
            dojahClient);

        var result = await service.GetKycAsync(kyc.Id);

        Assert.Equal(kyc.Id, result.Id);
        Assert.Equal(kyc.UserId, result.UserId);
        Assert.Equal(KycStatus.Rejected, result.Status);
        Assert.Equal("Failed", result.ProviderStatus);
        Assert.Equal("Identity checks failed.", result.RejectionReason);
        Assert.Null(result.DojahDetails);
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
    public async Task DriverActivationAndDeactivationUseSeparateNotificationTypes()
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
        var email = new FakeEmailService();
        var service = CreateService(unitOfWork, email);

        await service.ActivateDriverAsync(driver.Id);
        await service.ActivateDriverAsync(driver.Id);
        await service.DeactivateDriverAsync(driver.Id);
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
                notification.Type == NotificationType.DriverDeactivated &&
                notification.Title == "Driver account deactivated" &&
                notification.Message == "Your driver account was deactivated." &&
                notification.DeduplicationKey ==
                    $"driver-deactivated:{driver.Id}");
        Assert.Single(
            email.Messages,
            message => message.Subject ==
                "Your Pryde driver onboarding is approved");
        Assert.Single(
            email.Messages,
            message => message.Subject ==
                "Your Pryde driver account was deactivated");
        Assert.Single(
            unitOfWork.NotificationRepository.Items,
            notification => notification.Type ==
                NotificationType.DriverApproved);
        Assert.Single(
            unitOfWork.NotificationRepository.Items,
            notification => notification.Type ==
                NotificationType.DriverDeactivated);
    }

    [Fact]
    public async Task PassengerDetailIncludesProfilePhotoAndExistingFields()
    {
        var unitOfWork = new TestUnitOfWork();
        var passenger = new User
        {
            Email = "passenger.detail@test.local",
            PhoneNumber = "08000000002",
            Status = UserStatus.Active,
            IsEmailVerified = true,
            Profile = new Profile
            {
                FirstName = "Passenger",
                LastName = "Detail",
                ProfilePhotoUrl = "https://media.test/passenger-detail.jpg"
            }
        };
        var role = new Role { Name = "Passenger" };
        passenger.UserRoles.Add(new UserRole
        {
            User = passenger,
            UserId = passenger.Id,
            Role = role,
            RoleId = role.Id
        });
        passenger.KycVerification = new KycVerification
        {
            User = passenger,
            UserId = passenger.Id,
            Status = KycStatus.Approved
        };
        unitOfWork.UserRepository.Items.Add(passenger);
        var service = CreateService(unitOfWork, new FakeEmailService());

        var result = await service.GetUserAsync(passenger.Id);

        Assert.Equal("https://media.test/passenger-detail.jpg", result.ProfilePhotoUrl);
        Assert.Equal(passenger.Email, result.Email);
        Assert.Equal(passenger.PhoneNumber, result.PhoneNumber);
        Assert.Equal("Passenger Detail", result.FullName);
        Assert.True(result.IsEmailVerified);
        Assert.Equal("Approved", result.KycStatus);
        Assert.NotNull(result.Kyc);
        Assert.Contains("Passenger", result.Roles);
    }

    [Fact]
    public async Task DriverListingIncludesProfilePhoto()
    {
        var unitOfWork = new TestUnitOfWork();
        var driver = CreateDriver("driver.list@test.local", "https://media.test/driver-list.jpg");
        unitOfWork.UserRepository.Items.Add(driver);
        var service = CreateService(unitOfWork, new FakeEmailService());

        var result = await service.GetDriversAsync(new AdminDriversRequestDto());

        var item = Assert.Single(result.Items);
        Assert.Equal(driver.Id, item.Id);
        Assert.Equal("https://media.test/driver-list.jpg", item.ProfilePhotoUrl);
        Assert.Equal(driver.Email, item.Email);
        Assert.Contains("Driver", item.Roles);
    }

    [Fact]
    public async Task DriverDetailIncludesProfilePhotoRatingSummaryAndExistingFields()
    {
        var unitOfWork = new TestUnitOfWork();
        var driver = CreateDriver(
            "driver.detail@test.local",
            "https://media.test/driver-detail.jpg");
        driver.KycVerification = new KycVerification
        {
            User = driver,
            UserId = driver.Id,
            Status = KycStatus.Approved
        };
        var vehicle = new Vehicle
        {
            User = driver,
            UserId = driver.Id,
            LicensePlateNumber = "PRYDE-01",
            Make = "Toyota",
            Model = "Camry",
            Capacity = 4,
            IsActive = true
        };
        vehicle.Images.Add(new VehicleImage
        {
            Vehicle = vehicle,
            VehicleId = vehicle.Id,
            ImageUrl = "https://media.test/vehicle.jpg",
            IsPrimary = true
        });
        vehicle.Documents.Add(new VehicleDocument
        {
            Vehicle = vehicle,
            VehicleId = vehicle.Id,
            DocumentType = VehicleDocumentType.Insurance,
            DocumentUrl = "https://media.test/insurance.pdf",
            ReviewStatus = VehicleDocumentReviewStatus.Approved
        });
        driver.Vehicles.Add(vehicle);
        unitOfWork.UserRepository.Items.Add(driver);
        unitOfWork.TripRatingRepository.Items.AddRange(
        [
            new TripRating
            {
                BookingId = Guid.NewGuid(),
                RaterId = Guid.NewGuid(),
                RatedUserId = driver.Id,
                Value = 4
            },
            new TripRating
            {
                BookingId = Guid.NewGuid(),
                RaterId = Guid.NewGuid(),
                RatedUserId = driver.Id,
                Value = 5
            },
            new TripRating
            {
                BookingId = Guid.NewGuid(),
                RaterId = driver.Id,
                RatedUserId = Guid.NewGuid(),
                Value = 1
            }
        ]);
        var service = CreateService(unitOfWork, new FakeEmailService());

        var result = await service.GetDriverAsync(driver.Id);

        Assert.Equal("https://media.test/driver-detail.jpg", result.ProfilePhotoUrl);
        Assert.Equal(4.5, result.AverageRating);
        Assert.Equal(2, result.RatingCount);
        Assert.Equal(1, unitOfWork.TripRatingRepository.SummaryQueryCount);
        Assert.Equal(driver.Email, result.Email);
        Assert.Equal("Approved", result.KycStatus);
        Assert.NotNull(result.Kyc);
        var mappedVehicle = Assert.Single(result.Vehicles);
        Assert.Equal("PRYDE-01", mappedVehicle.LicensePlateNumber);
        Assert.Single(mappedVehicle.Images);
        Assert.Single(mappedVehicle.Documents);
        Assert.Equal("Approved", result.VehicleDocumentStatus);
        Assert.NotNull(result.TripSummary);
    }

    [Fact]
    public async Task DriverDetailWithoutRatingsOrProfilePhotoReturnsDefaults()
    {
        var unitOfWork = new TestUnitOfWork();
        var driver = CreateDriver("driver.defaults@test.local", null);
        unitOfWork.UserRepository.Items.Add(driver);
        var service = CreateService(unitOfWork, new FakeEmailService());

        var result = await service.GetDriverAsync(driver.Id);

        Assert.Null(result.ProfilePhotoUrl);
        Assert.Equal(0, result.AverageRating);
        Assert.Equal(0, result.RatingCount);
        Assert.Equal(1, unitOfWork.TripRatingRepository.SummaryQueryCount);
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

    private static User CreateDriver(string email, string? profilePhotoUrl)
    {
        var user = new User
        {
            Email = email,
            PhoneNumber = "08000000003",
            Status = UserStatus.Active,
            Profile = new Profile
            {
                FirstName = "Test",
                LastName = "Driver",
                ProfilePhotoUrl = profilePhotoUrl
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
