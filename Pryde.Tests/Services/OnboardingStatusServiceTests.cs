using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class OnboardingStatusServiceTests
{
    [Fact]
    public async Task NoCustomerRoleRequiresRoleSelection()
    {
        var (unitOfWork, user) = Context();

        var result = await Service(unitOfWork).GetAsync(user.Id);

        Assert.Empty(result.Roles);
        Assert.Equal(OnboardingStage.RoleSelection, result.CurrentStage);
        Assert.Equal(OnboardingStage.RoleSelection, result.NextStage);
        Assert.False(result.OnboardingCompleted);
    }

    [Fact]
    public async Task PassengerWithApprovedKycIsCompleted()
    {
        var (unitOfWork, user) = Context(RoleNames.Passenger);
        ApproveKyc(unitOfWork, user.Id);

        var result = await Service(unitOfWork).GetAsync(user.Id);

        Assert.Equal(OnboardingStage.Completed, result.CurrentStage);
        Assert.Equal(
            [OnboardingStage.RoleSelection, OnboardingStage.IdentityVerification],
            result.CompletedStages);
        Assert.True(result.OnboardingCompleted);
        Assert.False(result.DriverAccessGranted);
    }

    [Fact]
    public async Task PassengerAndDriverRolesAreReturnedWhileDriverFlowTakesPrecedence()
    {
        var (unitOfWork, user) = Context(
            RoleNames.Passenger,
            RoleNames.Driver);
        ApproveKyc(unitOfWork, user.Id);

        var result = await Service(unitOfWork).GetAsync(user.Id);

        Assert.Equal(
            [RoleNames.Passenger, RoleNames.Driver],
            result.Roles);
        Assert.Equal(OnboardingStage.DriverDocuments, result.CurrentStage);
        Assert.Equal(
            DriverVerificationStatus.NotSubmitted,
            result.DriverVerificationStatus);
    }

    [Fact]
    public async Task DojahApprovedDriverWithNullManualUrlsProceedsToVehicleOnboarding()
    {
        var (unitOfWork, user) = Context(RoleNames.Driver);
        var kyc = new KycVerification
        {
            UserId = user.Id,
            Status = KycStatus.Approved,
            ProviderName = "Dojah",
            ProviderStatus = "Completed",
            ProviderReference = "PRYDE-correlation",
            DojahReference = "provider-generated-reference"
        };
        unitOfWork.KycVerificationRepository.Items.Add(kyc);

        var result = await Service(unitOfWork).GetAsync(user.Id);

        Assert.Null(kyc.BiometricVerificationUrl);
        Assert.Null(kyc.DriverLicenseUrl);
        Assert.Null(kyc.SecondaryIdentificationUrl);
        Assert.Equal(OnboardingStage.DriverDocuments, result.CurrentStage);
        Assert.Contains(
            OnboardingStage.IdentityVerification,
            result.CompletedStages);
        Assert.Equal(OnboardingStage.DriverDocuments, result.NextStage);
    }

    [Fact]
    public async Task VehicleRegistrationCompletesDriverDocumentsStage()
    {
        var (unitOfWork, user) = Context(RoleNames.Driver);
        ApproveKyc(unitOfWork, user.Id);
        var vehicle = AddVehicle(
            unitOfWork,
            user.Id,
            VehicleOnboardingStatus.Draft);
        unitOfWork.VehicleDocumentRepository.Items.Add(new VehicleDocument
        {
            VehicleId = vehicle.Id,
            DocumentType = VehicleDocumentType.VehicleRegistration,
            DocumentUrl = "https://files.test/registration.pdf"
        });

        var result = await Service(unitOfWork).GetAsync(user.Id);

        Assert.Equal(OnboardingStage.VehicleInformation, result.CurrentStage);
        Assert.Contains(
            OnboardingStage.DriverDocuments,
            result.CompletedStages);
        Assert.Equal(OnboardingStage.VehicleInformation, result.NextStage);
    }

    [Fact]
    public async Task PendingVehicleIsSubmittedButDriverAccessIsNotGranted()
    {
        var (unitOfWork, user) = Context(RoleNames.Driver);
        ApproveKyc(unitOfWork, user.Id);
        AddVehicle(
            unitOfWork,
            user.Id,
            VehicleOnboardingStatus.PendingReview);

        var result = await Service(unitOfWork).GetAsync(user.Id);

        Assert.Equal(
            OnboardingStage.SubmittedForReview,
            result.CurrentStage);
        Assert.Equal(
            DriverVerificationStatus.Pending,
            result.DriverVerificationStatus);
        Assert.True(result.OnboardingCompleted);
        Assert.False(result.DriverAccessGranted);
        Assert.Null(result.NextStage);
    }

    [Fact]
    public async Task RejectedVehicleRequiresResubmissionAndReturnsReason()
    {
        var (unitOfWork, user) = Context(RoleNames.Driver);
        ApproveKyc(unitOfWork, user.Id);
        var vehicle = AddVehicle(
            unitOfWork,
            user.Id,
            VehicleOnboardingStatus.Rejected);
        vehicle.RejectionReason = "Registration image is unreadable.";

        var result = await Service(unitOfWork).GetAsync(user.Id);

        Assert.Equal(
            OnboardingStage.SubmittedForReview,
            result.CurrentStage);
        Assert.Equal(
            DriverVerificationStatus.ResubmissionRequired,
            result.DriverVerificationStatus);
        Assert.Equal(
            vehicle.RejectionReason,
            result.RejectionReason);
        Assert.False(result.DriverAccessGranted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ApprovedVehicleCompletesOnboardingAndUsesActivationForAccess(
        bool isActive)
    {
        var (unitOfWork, user) = Context(RoleNames.Driver);
        ApproveKyc(unitOfWork, user.Id);
        var vehicle = AddVehicle(
            unitOfWork,
            user.Id,
            VehicleOnboardingStatus.Approved);
        vehicle.IsActive = isActive;

        var result = await Service(unitOfWork).GetAsync(user.Id);

        Assert.Equal(OnboardingStage.Completed, result.CurrentStage);
        Assert.Equal(
            DriverVerificationStatus.Approved,
            result.DriverVerificationStatus);
        Assert.True(result.OnboardingCompleted);
        Assert.Equal(isActive, result.DriverAccessGranted);
    }

    [Fact]
    public async Task KycRejectionKeepsIdentityStageAndReturnsReason()
    {
        var (unitOfWork, user) = Context(RoleNames.Driver);
        unitOfWork.KycVerificationRepository.Items.Add(new KycVerification
        {
            UserId = user.Id,
            Status = KycStatus.Rejected,
            RejectionReason = "Identity document did not match."
        });

        var result = await Service(unitOfWork).GetAsync(user.Id);

        Assert.Equal(
            OnboardingStage.IdentityVerification,
            result.CurrentStage);
        Assert.Equal(KycStatus.Rejected, result.KycStatus);
        Assert.Equal(
            "Identity document did not match.",
            result.RejectionReason);
    }

    private static OnboardingStatusService Service(
        TestUnitOfWork unitOfWork) =>
        new(unitOfWork);

    private static (TestUnitOfWork UnitOfWork, User User) Context(
        params string[] roles)
    {
        var unitOfWork = new TestUnitOfWork();
        var user = new User
        {
            Email = "onboarding@test.local",
            PhoneNumber = "08000000000",
            IsEmailVerified = true,
            Status = UserStatus.Active
        };
        ((TestUserRepository)unitOfWork.Users).Items.Add(user);

        foreach (var roleName in roles)
        {
            unitOfWork.UserRoleRepository.Items.Add(new UserRole
            {
                UserId = user.Id,
                Role = new Role { Name = roleName }
            });
        }

        return (unitOfWork, user);
    }

    private static void ApproveKyc(
        TestUnitOfWork unitOfWork,
        Guid userId)
    {
        unitOfWork.KycVerificationRepository.Items.Add(new KycVerification
        {
            UserId = userId,
            Status = KycStatus.Approved
        });
    }

    private static Vehicle AddVehicle(
        TestUnitOfWork unitOfWork,
        Guid userId,
        VehicleOnboardingStatus status)
    {
        var vehicle = new Vehicle
        {
            UserId = userId,
            LicensePlateNumber = $"PRYDE-{Guid.NewGuid():N}",
            OnboardingStatus = status
        };
        unitOfWork.VehicleRepository.Items.Add(vehicle);
        return vehicle;
    }
}
