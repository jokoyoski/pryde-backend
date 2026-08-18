using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pryde.Persistence.Context;
using Pryde.Services.Notifications.Interface;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Settings;
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
    public async Task SuccessfulVehicleSubmissionWithAllRequiredDocumentsMovesToPendingReview()
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

    [Theory]
    [InlineData(VehicleDocumentType.VehicleRegistration)]
    [InlineData(VehicleDocumentType.Insurance)]
    [InlineData(VehicleDocumentType.RoadworthinessCertificate)]
    public async Task MissingRequiredVehicleDocumentBlocksSubmission(
        VehicleDocumentType missingDocumentType)
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        Complete(unitOfWork, vehicle);
        unitOfWork.VehicleDocumentRepository.Items.RemoveAll(document =>
            document.VehicleId == vehicle.Id &&
            document.DocumentType == missingDocumentType);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(unitOfWork).SubmitAsync(vehicle.Id, ownerId));

        Assert.Contains(
            "Vehicle onboarding is incomplete",
            exception.Message);
        Assert.Equal(VehicleOnboardingStatus.Draft, vehicle.OnboardingStatus);
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
        SetRequiredDocumentReviewStatus(
            unitOfWork,
            vehicle.Id,
            VehicleDocumentReviewStatus.Approved);
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
        SetRequiredDocumentReviewStatus(
            unitOfWork,
            vehicle.Id,
            VehicleDocumentReviewStatus.Approved);
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
        var email = new CapturingEmailService();
        var service = Service(unitOfWork, email);

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
        var rejectionEmail = Assert.Single(email.Messages);
        Assert.Equal(
            "Your Pryde driver onboarding requires attention",
            rejectionEmail.Subject);
        Assert.Contains("Incorrect registration", rejectionEmail.Body);
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
    public async Task VehicleActivationFailsWhenRequiredDocumentIsPending()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        Complete(unitOfWork, vehicle);
        SetRequiredDocumentReviewStatus(
            unitOfWork,
            vehicle.Id,
            VehicleDocumentReviewStatus.Approved);
        RequiredDocument(
            unitOfWork,
            vehicle.Id,
            VehicleDocumentType.Insurance).ReviewStatus =
            VehicleDocumentReviewStatus.Pending;
        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        AddApprovedKyc(unitOfWork, ownerId);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(unitOfWork).ActivateAsync(vehicle.Id));

        Assert.Contains("not approved: insurance", exception.Message);
        Assert.False(vehicle.IsActive);
    }

    [Fact]
    public async Task VehicleActivationFailsWhenRequiredDocumentIsRejected()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        Complete(unitOfWork, vehicle);
        SetRequiredDocumentReviewStatus(
            unitOfWork,
            vehicle.Id,
            VehicleDocumentReviewStatus.Approved);
        RequiredDocument(
            unitOfWork,
            vehicle.Id,
            VehicleDocumentType.RoadworthinessCertificate).ReviewStatus =
            VehicleDocumentReviewStatus.Rejected;
        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        AddApprovedKyc(unitOfWork, ownerId);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(unitOfWork).ActivateAsync(vehicle.Id));

        Assert.Contains(
            "not approved: roadworthiness certificate",
            exception.Message);
        Assert.False(vehicle.IsActive);
    }

    [Fact]
    public async Task VehicleActivationFailsWhenRequiredDocumentIsMissing()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        Complete(unitOfWork, vehicle);
        SetRequiredDocumentReviewStatus(
            unitOfWork,
            vehicle.Id,
            VehicleDocumentReviewStatus.Approved);
        unitOfWork.VehicleDocumentRepository.Items.RemoveAll(document =>
            document.VehicleId == vehicle.Id &&
            document.DocumentType ==
            VehicleDocumentType.VehicleRegistration);
        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        AddApprovedKyc(unitOfWork, ownerId);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(unitOfWork).ActivateAsync(vehicle.Id));

        Assert.Contains("missing: vehicle registration", exception.Message);
        Assert.False(vehicle.IsActive);
    }

    [Fact]
    public async Task VehicleActivationSucceedsWhenAllRequiredDocumentsAreApproved()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        Complete(unitOfWork, vehicle);
        SetRequiredDocumentReviewStatus(
            unitOfWork,
            vehicle.Id,
            VehicleDocumentReviewStatus.Approved);
        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        AddApprovedKyc(unitOfWork, ownerId);

        var result = await Service(unitOfWork).ActivateAsync(vehicle.Id);

        Assert.True(result.IsActive);
        Assert.Equal(VehicleOnboardingStatus.Approved, result.OnboardingStatus);
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
        var service = DocumentService(unitOfWork);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UploadAsync(
                vehicle.Id,
                ownerId,
                (VehicleDocumentType)999,
                null,
                "https://files.test/invalid.pdf"));
    }

    [Theory]
    [InlineData(VehicleDocumentType.VehicleRegistration)]
    [InlineData(VehicleDocumentType.Insurance)]
    [InlineData(VehicleDocumentType.RoadworthinessCertificate)]
    public async Task RequiredVehicleDocumentRequiresExpiry(
        VehicleDocumentType documentType)
    {
        var (unitOfWork, ownerId, vehicle) = Context();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            DocumentService(unitOfWork).UploadAsync(
                vehicle.Id,
                ownerId,
                documentType,
                null,
                "https://files.test/document.pdf"));

        Assert.Equal(
            $"Expiry date is required for {documentType}.",
            exception.Message);
    }

    [Theory]
    [InlineData(VehicleDocumentType.VehicleRegistration)]
    [InlineData(VehicleDocumentType.Insurance)]
    [InlineData(VehicleDocumentType.RoadworthinessCertificate)]
    public async Task ConfiguredSixMonthMinimumIsEnforced(
        VehicleDocumentType documentType)
    {
        var (unitOfWork, ownerId, vehicle) = Context();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            DocumentService(unitOfWork, 6).UploadAsync(
                vehicle.Id,
                ownerId,
                documentType,
                DateTime.UtcNow.Date.AddMonths(6).AddDays(-1),
                "https://files.test/document.pdf"));

        Assert.Equal(
            $"{documentType} must have at least 6 months remaining validity.",
            exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task RequiredVehicleDocumentWithAtLeastSixMonthsValidityIsAccepted(
        int additionalMonths)
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        var expiryDate = DateTime.UtcNow.Date.AddMonths(
            6 + additionalMonths);

        var result = await DocumentService(unitOfWork, 6)
            .UploadAsync(
                vehicle.Id,
                ownerId,
                VehicleDocumentType.VehicleRegistration,
                expiryDate,
                "https://files.test/registration.pdf");

        Assert.Equal(expiryDate, result.ExpiryDate);
    }

    [Fact]
    public async Task ConfiguredTwoMonthMinimumChangesValidityValidation()
    {
        var (unitOfWork, ownerId, vehicle) = Context();
        var service = DocumentService(unitOfWork, 2);

        var rejected = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UploadAsync(
                vehicle.Id,
                ownerId,
                VehicleDocumentType.Insurance,
                DateTime.UtcNow.Date.AddMonths(2).AddDays(-1),
                "https://files.test/insurance.pdf"));
        var accepted = await service.UploadAsync(
            vehicle.Id,
            ownerId,
            VehicleDocumentType.Insurance,
            DateTime.UtcNow.Date.AddMonths(2),
            "https://files.test/insurance.pdf");

        Assert.Equal(
            "Insurance must have at least 2 months remaining validity.",
            rejected.Message);
        Assert.Equal(
            DateTime.UtcNow.Date.AddMonths(2),
            accepted.ExpiryDate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidMinimumValidityMonthsFailsOptionsValidation(
        int minimumValidityMonths)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{VehicleDocumentSettings.SectionName}:MinimumValidityMonths"] =
                    minimumValidityMonths.ToString()
            })
            .Build();
        var services = new ServiceCollection();
        services
            .AddOptions<VehicleDocumentSettings>()
            .Bind(configuration.GetSection(
                VehicleDocumentSettings.SectionName))
            .Validate(
                VehicleDocumentSettings.IsValid,
                VehicleDocumentSettings.ValidationError)
            .ValidateOnStart();
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<
                IOptions<VehicleDocumentSettings>>().Value);

        Assert.Contains(
            VehicleDocumentSettings.ValidationError,
            exception.Failures);
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

    private static VehicleService Service(
        TestUnitOfWork unitOfWork,
        IEmailService? emailService = null) =>
        new(
            unitOfWork,
            null!,
            null!,
            NullLogger<VehicleService>.Instance,
            new NotificationService(unitOfWork),
            emailService ?? new CapturingEmailService());

    private sealed class CapturingEmailService : IEmailService
    {
        public List<(string Subject, string Body)> Messages { get; } = [];

        public Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
        {
            Messages.Add((subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    private static VehicleDocumentService DocumentService(
        TestUnitOfWork unitOfWork,
        int minimumValidityMonths = 6) =>
        new(
            unitOfWork,
            Options.Create(new VehicleDocumentSettings
            {
                MinimumValidityMonths = minimumValidityMonths
            }));

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
        foreach (var documentType in new[]
                 {
                     VehicleDocumentType.VehicleRegistration,
                     VehicleDocumentType.Insurance,
                     VehicleDocumentType.RoadworthinessCertificate
                 })
        {
            unitOfWork.VehicleDocumentRepository.Items.Add(new VehicleDocument
            {
                VehicleId = vehicle.Id,
                DocumentType = documentType,
                DocumentUrl = $"https://files.test/{documentType}.pdf",
                ExpiryDate = DateTime.UtcNow.AddYears(1)
            });
        }
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

    private static void SetRequiredDocumentReviewStatus(
        TestUnitOfWork unitOfWork,
        Guid vehicleId,
        VehicleDocumentReviewStatus reviewStatus)
    {
        foreach (var document in unitOfWork.VehicleDocumentRepository.Items
                     .Where(document => document.VehicleId == vehicleId))
        {
            document.ReviewStatus = reviewStatus;
        }
    }

    private static VehicleDocument RequiredDocument(
        TestUnitOfWork unitOfWork,
        Guid vehicleId,
        VehicleDocumentType documentType) =>
        unitOfWork.VehicleDocumentRepository.Items.Single(document =>
            document.VehicleId == vehicleId &&
            document.DocumentType == documentType);

    private static void AddApprovedKyc(
        TestUnitOfWork unitOfWork,
        Guid ownerId)
    {
        unitOfWork.KycVerificationRepository.Items.Add(new KycVerification
        {
            UserId = ownerId,
            Status = KycStatus.Approved
        });
    }
}
