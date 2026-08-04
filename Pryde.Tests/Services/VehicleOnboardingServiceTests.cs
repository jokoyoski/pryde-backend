using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pryde.Persistence.Context;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class VehicleOnboardingServiceTests
{
    [Fact]
    public void TypedImagesAndAmenitiesHaveDatabaseUniquenessConstraints()
    {
        var options = new DbContextOptionsBuilder<PrydeDbContext>()
            .UseNpgsql("Host=localhost;Database=pryde-model;Username=test;Password=test")
            .Options;
        using var context = new PrydeDbContext(options);

        var imageIndexes = context.Model.FindEntityType(typeof(VehicleImage))!
            .GetIndexes();
        var amenityIndexes = context.Model.FindEntityType(typeof(VehicleAmenity))!
            .GetIndexes();

        Assert.Contains(
            imageIndexes,
            index => index.IsUnique &&
                     index.Properties.Select(x => x.Name)
                         .SequenceEqual([nameof(VehicleImage.VehicleId), nameof(VehicleImage.ImageType)]));
        Assert.Contains(
            amenityIndexes,
            index => index.IsUnique &&
                     index.Properties.Select(x => x.Name)
                         .SequenceEqual([nameof(VehicleAmenity.VehicleId), nameof(VehicleAmenity.AmenityType)]));
    }

    [Fact]
    public async Task DraftVehicleCanBeCreatedBeforeKycApproval()
    {
        var unitOfWork = new TestUnitOfWork();
        var ownerId = Guid.NewGuid();
        unitOfWork.UserRoleRepository.Items.Add(new UserRole
        {
            UserId = ownerId,
            Role = new Role { Name = RoleType.Driver.ToString() }
        });

        var result = await Service(unitOfWork).CreateAsync(
            ownerId, "PRYDE-DRAFT", 0, []);

        Assert.Equal(VehicleOnboardingStatus.Draft, result.OnboardingStatus);
        Assert.False(result.IsActive);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task ValidPassengerSeatCountsAreAccepted(int seatCount)
    {
        var (unitOfWork, ownerId, vehicle) = Context();

        var result = await Service(unitOfWork).UpdateCapacityExtrasAsync(
            vehicle.Id,
            ownerId,
            CapacityRequest(seatCount));

        Assert.Equal(seatCount, result.PassengerSeatCount);
        Assert.Equal(seatCount, result.Capacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    public async Task InvalidPassengerSeatCountsAreRejected(int seatCount)
    {
        var (unitOfWork, ownerId, vehicle) = Context();

        await Assert.ThrowsAsync<ValidationException>(() =>
            Service(unitOfWork).UpdateCapacityExtrasAsync(
                vehicle.Id,
                ownerId,
                CapacityRequest(seatCount)));
    }

    [Fact]
    public async Task TypedImageUpdateReplacesInsteadOfDuplicating()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        var service = Service(unitOfWork);

        await service.UpdateMediaAsync(
            vehicle.Id,
            ownerId,
            new Dictionary<VehicleImageType, string>
            {
                [VehicleImageType.FrontView] = "https://files.test/front-old.jpg"
            },
            null);
        await service.UpdateMediaAsync(
            vehicle.Id,
            ownerId,
            new Dictionary<VehicleImageType, string>
            {
                [VehicleImageType.FrontView] = "https://files.test/front-new.jpg"
            },
            null);

        var image = Assert.Single(unitOfWork.VehicleImageRepository.Items);
        Assert.Equal("https://files.test/front-new.jpg", image.ImageUrl);
    }

    [Fact]
    public async Task DuplicateAmenitiesAreNotCreated()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        var service = Service(unitOfWork);
        var request = new VehicleCapacityExtrasRequestDto
        {
            PassengerSeatCount = 4,
            LuggageCapacity = LuggageCapacity.Medium,
            Amenities =
            [
                VehicleAmenityType.AirConditioning,
                VehicleAmenityType.AirConditioning,
                VehicleAmenityType.ChargingPort
            ]
        };

        await service.UpdateCapacityExtrasAsync(vehicle.Id, ownerId, request);
        await service.UpdateCapacityExtrasAsync(vehicle.Id, ownerId, request);

        Assert.Equal(2, unitOfWork.VehicleAmenityRepository.Items.Count);
        Assert.Equal(
            2,
            unitOfWork.VehicleAmenityRepository.Items
                .Select(x => x.AmenityType)
                .Distinct()
                .Count());
    }

    [Fact]
    public async Task AnotherUserCannotUpdateVehicle()
    {
        var (unitOfWork, _, vehicle) = Context();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Service(unitOfWork).UpdateDetailsAsync(
                vehicle.Id,
                Guid.NewGuid(),
                DetailsRequest()));
    }

    [Fact]
    public async Task IncompleteVehicleCannotBeSubmitted()
    {
        var (unitOfWork, ownerId, vehicle) = Context();

        await Assert.ThrowsAsync<ValidationException>(() =>
            Service(unitOfWork).SubmitAsync(vehicle.Id, ownerId));
    }

    [Fact]
    public async Task CompleteVehicleMovesToPendingReview()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        Complete(unitOfWork, vehicle);

        var result = await Service(unitOfWork).SubmitAsync(vehicle.Id, ownerId);

        Assert.Equal(VehicleOnboardingStatus.PendingReview, result.OnboardingStatus);
        Assert.False(result.IsActive);
        Assert.Equal(
            VehicleOnboardingStatus.PendingReview,
            result.WorkflowStatus);
        Assert.Equal(
            WorkflowNextAction.AwaitAdminApproval,
            result.NextAction);
        Assert.Equal(WorkflowActor.Admin, result.RequiredActor);
    }

    [Fact]
    public async Task NullManualKycUrlsDoNotBlockVehicleUpdateOrSubmission()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        unitOfWork.KycVerificationRepository.Items.Add(new KycVerification
        {
            UserId = ownerId,
            Status = KycStatus.Approved,
            ProviderName = "Dojah",
            ProviderStatus = "Completed",
            ProviderReference = "PRYDE-correlation",
            DojahReference = "provider-generated-reference"
        });
        var service = Service(unitOfWork);

        await service.UpdateDetailsAsync(
            vehicle.Id,
            ownerId,
            DetailsRequest());
        Complete(unitOfWork, vehicle);
        var result = await service.SubmitAsync(vehicle.Id, ownerId);

        Assert.Equal(
            VehicleOnboardingStatus.PendingReview,
            result.OnboardingStatus);
    }

    [Theory]
    [InlineData("VehicleType")]
    [InlineData("Make")]
    [InlineData("Model")]
    [InlineData("ManufacturingYear")]
    [InlineData("Colour")]
    public async Task MissingCoreVehicleFieldBlocksSubmission(
        string field)
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        Complete(unitOfWork, vehicle);

        switch (field)
        {
            case "VehicleType":
                vehicle.VehicleType = null;
                break;
            case "Make":
                vehicle.Make = null;
                break;
            case "Model":
                vehicle.Model = null;
                break;
            case "ManufacturingYear":
                vehicle.ManufacturingYear = null;
                break;
            case "Colour":
                vehicle.Colour = null;
                break;
        }

        await Assert.ThrowsAsync<ValidationException>(() =>
            Service(unitOfWork).SubmitAsync(vehicle.Id, ownerId));
    }

    [Fact]
    public async Task PendingReviewVehicleCannotBeEdited()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;

        await Assert.ThrowsAsync<ConflictException>(() =>
            Service(unitOfWork).UpdateDetailsAsync(
                vehicle.Id,
                ownerId,
                DetailsRequest()));
    }

    [Fact]
    public async Task RejectedVehicleCanBeEditedAndResubmitted()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        Complete(unitOfWork, vehicle);
        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        var service = Service(unitOfWork);

        await service.RejectAsync(vehicle.Id, "Incorrect colour");
        await service.UpdateDetailsAsync(
            vehicle.Id,
            ownerId,
            DetailsRequest("Black"));
        var result = await service.SubmitAsync(vehicle.Id, ownerId);

        Assert.Equal(VehicleOnboardingStatus.PendingReview, result.OnboardingStatus);
        Assert.Null(result.RejectionReason);
        Assert.Equal("Black", result.Colour);
    }

    [Fact]
    public async Task ApprovedVehicleCannotBeEditedByDriver()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        vehicle.OnboardingStatus = VehicleOnboardingStatus.Approved;

        await Assert.ThrowsAsync<ConflictException>(() =>
            Service(unitOfWork).UpdateDetailsAsync(
                vehicle.Id,
                ownerId,
                DetailsRequest()));
    }

    [Fact]
    public async Task AdminActivationRequiresApprovedKyc()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        Complete(unitOfWork, vehicle);
        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        var service = Service(unitOfWork);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ActivateAsync(vehicle.Id));

        unitOfWork.KycVerificationRepository.Items.Add(new KycVerification
        {
            UserId = ownerId,
            Status = KycStatus.Approved,
            ProviderName = "Dojah",
            ProviderStatus = "Completed",
            ProviderReference = "PRYDE-correlation",
            DojahReference = "provider-generated-reference"
        });
        var result = await service.ActivateAsync(vehicle.Id);

        Assert.True(result.IsActive);
        Assert.Equal(VehicleOnboardingStatus.Approved, result.OnboardingStatus);
        Assert.Equal(WorkflowNextAction.CreateTrip, result.NextAction);
        Assert.Equal(WorkflowActor.Driver, result.RequiredActor);
    }

    [Theory]
    [InlineData(KycStatus.Pending)]
    [InlineData(KycStatus.Rejected)]
    public async Task PendingOrRejectedKycBlocksVehicleApproval(
        KycStatus kycStatus)
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        Complete(unitOfWork, vehicle);
        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        unitOfWork.KycVerificationRepository.Items.Add(new KycVerification
        {
            UserId = ownerId,
            Status = kycStatus,
            ProviderName = "Dojah"
        });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Service(unitOfWork).ActivateAsync(vehicle.Id));

        Assert.Equal(
            VehicleOnboardingStatus.PendingReview,
            vehicle.OnboardingStatus);
        Assert.False(vehicle.IsActive);
    }

    [Fact]
    public async Task AdminRejectionRequiresReason()
    {
        var (unitOfWork, _, vehicle) = Context();
        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;

        await Assert.ThrowsAsync<ValidationException>(() =>
            Service(unitOfWork).RejectAsync(vehicle.Id, " "));
    }

    [Fact]
    public async Task AdminRejectionSetsRejectedAndInactive()
    {
        var (unitOfWork, _, vehicle) = Context();
        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        vehicle.IsActive = true;

        var result = await Service(unitOfWork)
            .RejectAsync(vehicle.Id, "  Missing registration  ");

        Assert.Equal(VehicleOnboardingStatus.Rejected, result.OnboardingStatus);
        Assert.False(result.IsActive);
        Assert.Equal("Missing registration", result.RejectionReason);
        Assert.Equal(
            WorkflowNextAction.CompleteVehicleOnboarding,
            result.NextAction);
        Assert.Equal(WorkflowActor.Driver, result.RequiredActor);
    }

    [Fact]
    public async Task DriverApplicationRejectionTargetsOnlyNewestPendingVehicle()
    {
        var (unitOfWork, ownerId, olderVehicle) = Context();
        Complete(unitOfWork, olderVehicle);
        olderVehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        olderVehicle.CreatedAt = DateTime.UtcNow.AddMinutes(-1);
        var newerVehicle = new Vehicle
        {
            UserId = ownerId,
            LicensePlateNumber = "PRYDE-NEWEST",
            OnboardingStatus = VehicleOnboardingStatus.PendingReview,
            CreatedAt = DateTime.UtcNow
        };
        unitOfWork.VehicleRepository.Items.Add(newerVehicle);
        Complete(unitOfWork, newerVehicle);
        unitOfWork.KycVerificationRepository.Items.Add(new KycVerification
        {
            UserId = ownerId,
            Status = KycStatus.Approved
        });
        var service = Service(unitOfWork);

        var result = await service.RejectDriverApplicationAsync(
            ownerId,
            "Newest application rejected");

        Assert.Equal(newerVehicle.Id, result.Id);
        Assert.Equal(
            VehicleOnboardingStatus.Rejected,
            newerVehicle.OnboardingStatus);
        Assert.Equal(
            VehicleOnboardingStatus.PendingReview,
            olderVehicle.OnboardingStatus);
        Assert.Single(
            unitOfWork.NotificationRepository.Items,
            notification =>
                notification.RelatedEntityId == newerVehicle.Id);
    }

    [Fact]
    public async Task DriverApplicationRejectionRequiresReason()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;

        await Assert.ThrowsAsync<ValidationException>(() =>
            Service(unitOfWork)
                .RejectDriverApplicationAsync(ownerId, " "));
    }

    [Fact]
    public async Task DriverApplicationRejectionRequiresResubmissionAndBlocksTrips()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        Complete(unitOfWork, vehicle);
        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        vehicle.IsActive = true;
        unitOfWork.KycVerificationRepository.Items.Add(new KycVerification
        {
            UserId = ownerId,
            Status = KycStatus.Approved
        });

        var result = await Service(unitOfWork)
            .RejectDriverApplicationAsync(
                ownerId,
                "  Registration image is unreadable.  ");
        var onboarding = await new OnboardingStatusService(unitOfWork)
            .GetAsync(ownerId);

        Assert.Equal(VehicleOnboardingStatus.Rejected, result.OnboardingStatus);
        Assert.False(result.IsActive);
        Assert.Equal(
            "Registration image is unreadable.",
            result.RejectionReason);
        Assert.Equal(
            WorkflowNextAction.CompleteVehicleOnboarding,
            result.NextAction);
        Assert.Equal(WorkflowActor.Driver, result.RequiredActor);
        Assert.Equal(
            VehicleOnboardingStatus.Rejected,
            onboarding.DriverApplicationStatus);
        Assert.Equal(
            DriverVerificationStatus.ResubmissionRequired,
            onboarding.DriverVerificationStatus);
        Assert.Single(
            unitOfWork.NotificationRepository.Items,
            notification =>
                notification.Type == NotificationType.VehicleRejected);
        await Assert.ThrowsAsync<ConflictException>(() =>
            TestData.CreateTripService(unitOfWork).CreateAsync(
                ownerId,
                TestData.ValidTripRequest(vehicle.Id)));

        await Service(unitOfWork).UpdateDetailsAsync(
            vehicle.Id,
            ownerId,
            DetailsRequest("Black"));
        var resubmitted = await Service(unitOfWork)
            .SubmitAsync(vehicle.Id, ownerId);

        Assert.Equal(
            VehicleOnboardingStatus.PendingReview,
            resubmitted.OnboardingStatus);
        Assert.Null(resubmitted.RejectionReason);
    }

    [Fact]
    public async Task RepeatedDriverApplicationRejectionUsesExistingConflictBehavior()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        var service = Service(unitOfWork);

        var first = await service.RejectDriverApplicationAsync(
            ownerId,
            "Incorrect registration");
        Assert.Equal(
            VehicleOnboardingStatus.Rejected,
            first.OnboardingStatus);
        Assert.Single(
            unitOfWork.NotificationRepository.Items,
            notification =>
                notification.Type == NotificationType.VehicleRejected);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RejectDriverApplicationAsync(
                ownerId,
                "  Incorrect registration  "));
    }

    [Fact]
    public async Task DraftDriverApplicationCannotBeRejected()
    {
        var (unitOfWork, ownerId, _) = Context();
        var service = Service(unitOfWork);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RejectDriverApplicationAsync(
                ownerId,
                "Not ready for review"));
    }

    [Fact]
    public async Task IncompleteVehicleCannotBeApproved()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        unitOfWork.KycVerificationRepository.Items.Add(new KycVerification
        {
            UserId = ownerId,
            Status = KycStatus.Approved
        });

        await Assert.ThrowsAsync<ValidationException>(() =>
            Service(unitOfWork).ActivateAsync(vehicle.Id));
    }

    [Fact]
    public async Task LuggageCapacityIsOptionalForSubmission()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        Complete(unitOfWork, vehicle);
        vehicle.LuggageCapacity = null;

        var result = await Service(unitOfWork)
            .SubmitAsync(vehicle.Id, ownerId);

        Assert.Equal(VehicleOnboardingStatus.PendingReview, result.OnboardingStatus);
        Assert.Null(result.LuggageCapacity);
    }

    [Fact]
    public async Task InvalidVehicleDocumentTypeIsRejected()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        var service = new VehicleDocumentService(unitOfWork);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UploadAsync(
                vehicle.Id,
                ownerId,
                (VehicleDocumentType)999,
                null,
                "https://files.test/invalid.pdf"));
    }

    [Fact]
    public async Task VehicleRegistrationDoesNotRequireExpiry()
    {
        var (unitOfWork, ownerId, vehicle) = Context();

        var result = await new VehicleDocumentService(unitOfWork)
            .UploadAsync(
                vehicle.Id,
                ownerId,
                VehicleDocumentType.VehicleRegistration,
                null,
                "https://files.test/registration.pdf");

        Assert.Null(result.ExpiryDate);
    }

    [Fact]
    public async Task ExistingCapacityUpdateEndpointBehaviourRemainsFunctional()
    {
        var (unitOfWork, ownerId, vehicle) = Context();

        var result = await Service(unitOfWork).UpdateAsync(
            vehicle.Id, ownerId, 4);

        Assert.Equal(4, result.Capacity);
        Assert.Equal(4, result.PassengerSeatCount);
    }

    private static VehicleService Service(TestUnitOfWork unitOfWork) =>
        new(
            unitOfWork,
            null!,
            null!,
            NullLogger<VehicleService>.Instance);

    private static (TestUnitOfWork UnitOfWork, Guid OwnerId, Vehicle Vehicle) Context()
    {
        var unitOfWork = new TestUnitOfWork();
        var ownerId = Guid.NewGuid();
        unitOfWork.UserRepository.Items.Add(new User
        {
            Id = ownerId,
            Email = "driver@test.local",
            PhoneNumber = "08000000000",
            IsEmailVerified = true,
            Status = UserStatus.Pending
        });
        var vehicle = new Vehicle
        {
            UserId = ownerId,
            LicensePlateNumber = "PRYDE-ONBOARDING",
            OnboardingStatus = VehicleOnboardingStatus.Draft
        };
        unitOfWork.VehicleRepository.Items.Add(vehicle);
        unitOfWork.UserRoleRepository.Items.Add(new UserRole
        {
            UserId = ownerId,
            Role = new Role { Name = RoleType.Driver.ToString() }
        });
        return (unitOfWork, ownerId, vehicle);
    }

    private static VehicleDetailsRequestDto DetailsRequest(
        string colour = "Silver") => new()
    {
        VehicleOwnerName = "Driver Owner",
        RegistrationType = VehicleRegistrationType.Private,
        VehicleType = "Sedan",
        Make = "Toyota",
        Model = "Corolla",
        ManufacturingYear = 2022,
        Colour = colour
    };

    private static VehicleCapacityExtrasRequestDto CapacityRequest(int seatCount) =>
        new()
        {
            PassengerSeatCount = seatCount,
            LuggageCapacity = LuggageCapacity.Medium,
            Amenities = [VehicleAmenityType.AirConditioning],
            AdditionalDetails = "Clean vehicle"
        };

    private static void Complete(TestUnitOfWork unitOfWork, Vehicle vehicle)
    {
        vehicle.VehicleOwnerName = "Driver Owner";
        vehicle.RegistrationType = VehicleRegistrationType.Private;
        vehicle.VehicleType = "Sedan";
        vehicle.Make = "Toyota";
        vehicle.Model = "Corolla";
        vehicle.ManufacturingYear = 2022;
        vehicle.Colour = "Silver";
        vehicle.PassengerSeatCount = 4;
        vehicle.Capacity = 4;
        unitOfWork.VehicleDocumentRepository.Items.Add(new VehicleDocument
        {
            VehicleId = vehicle.Id,
            DocumentType = VehicleDocumentType.VehicleRegistration,
            DocumentUrl = "https://files.test/registration.pdf",
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        });
        foreach (var imageType in Enum.GetValues<VehicleImageType>())
        {
            unitOfWork.VehicleImageRepository.Items.Add(new VehicleImage
            {
                VehicleId = vehicle.Id,
                ImageType = imageType,
                ImageUrl = $"https://files.test/{imageType}.jpg"
            });
        }
    }
}
