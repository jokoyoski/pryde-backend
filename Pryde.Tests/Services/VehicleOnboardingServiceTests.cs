using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Microsoft.EntityFrameworkCore;
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
        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        var service = Service(unitOfWork);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ActivateAsync(vehicle.Id));

        unitOfWork.KycVerificationRepository.Items.Add(new KycVerification
        {
            UserId = ownerId,
            Status = KycStatus.Approved
        });
        var result = await service.ActivateAsync(vehicle.Id);

        Assert.True(result.IsActive);
        Assert.Equal(VehicleOnboardingStatus.Approved, result.OnboardingStatus);
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
        new(unitOfWork);

    private static (TestUnitOfWork UnitOfWork, Guid OwnerId, Vehicle Vehicle) Context()
    {
        var unitOfWork = new TestUnitOfWork();
        var ownerId = Guid.NewGuid();
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

    private static VehicleDetailsRequestDto DetailsRequest() => new()
    {
        VehicleOwnerName = "Driver Owner",
        RegistrationType = VehicleRegistrationType.Private
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
        vehicle.PassengerSeatCount = 4;
        vehicle.Capacity = 4;
        vehicle.LuggageCapacity = LuggageCapacity.Medium;
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
