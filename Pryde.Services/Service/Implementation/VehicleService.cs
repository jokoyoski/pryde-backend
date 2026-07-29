using Pryde.Domain.Common.Exceptions;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class VehicleService(IUnitOfWork unitOfWork) : IVehicleService
{
    private static readonly HashSet<int> AllowedPassengerSeatCounts = [2, 4, 5, 6, 7];

    public async Task<VehicleResponseDto> CreateAsync(
        Guid driverId, string licensePlateNumber, int capacity,
        List<string> imageUrls, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(licensePlateNumber))
            throw new ValidationException("License plate number is required.");
        if (capacity < 0)
            throw new ValidationException("Capacity cannot be negative.");

        var userRoles = await unitOfWork.UserRoles.GetByUserIdAsync(driverId, cancellationToken);
        var isDriver = userRoles.Any(ur => ur.Role.Name == RoleType.Driver.ToString());
        if (!isDriver)
            throw new ForbiddenException("Only users with the Driver role can register a vehicle.");

        var plate = licensePlateNumber.Trim();
        if (await unitOfWork.Vehicles.ExistsAsync(plate, cancellationToken))
            throw new ConflictException("A vehicle with this license plate is already registered.");

        var vehicle = new Vehicle
        {
            UserId = driverId,
            LicensePlateNumber = plate,
            Capacity = capacity,
            PassengerSeatCount = AllowedPassengerSeatCounts.Contains(capacity)
                ? capacity
                : null,
            OnboardingStatus = VehicleOnboardingStatus.Draft,
            IsActive = false
        };

        await unitOfWork.Vehicles.CreateAsync(vehicle, cancellationToken);

        for (var i = 0; i < (imageUrls?.Count ?? 0); i++)
        {
            await unitOfWork.VehicleImages.CreateAsync(new VehicleImage
            {
                VehicleId = vehicle.Id,
                ImageUrl = imageUrls![i].Trim(),
                IsPrimary = i == 0
            }, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CompleteVehicleOnboarding,
            WorkflowActor.Driver,
            cancellationToken);
    }

    public async Task<VehicleResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), id);
        return await BuildResponseAsync(vehicle, cancellationToken);
    }

    public async Task<IReadOnlyList<VehicleResponseDto>> GetMyVehiclesAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        var vehicles = await unitOfWork.Vehicles.GetByUserIdAsync(driverId, cancellationToken);
        var result = new List<VehicleResponseDto>();
        foreach (var vehicle in vehicles)
            result.Add(await BuildResponseAsync(vehicle, cancellationToken));
        return result;
    }

    public async Task<VehicleResponseDto> UpdateAsync(
        Guid vehicleId, Guid requestingUserId, int capacity, CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);

        ValidatePassengerSeatCount(capacity);

        vehicle.Capacity = capacity;
        vehicle.PassengerSeatCount = capacity;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CompleteVehicleOnboarding,
            WorkflowActor.Driver,
            cancellationToken);
    }

    public async Task<VehicleResponseDto> UpdateDetailsAsync(
        Guid vehicleId,
        Guid requestingUserId,
        VehicleDetailsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);
        ValidateVehicleDetails(request);

        vehicle.VehicleOwnerName = NormalizeRequiredText(
            request.VehicleOwnerName, "Vehicle owner name", 200);
        vehicle.RegistrationType = request.RegistrationType;
        vehicle.VehicleType = NormalizeRequiredText(
            request.VehicleType, "Vehicle type", 100);
        vehicle.Make = NormalizeRequiredText(
            request.Make, "Vehicle make", 100);
        vehicle.Model = NormalizeRequiredText(
            request.Model, "Vehicle model", 100);
        vehicle.ManufacturingYear = request.ManufacturingYear;
        vehicle.Colour = NormalizeRequiredText(
            request.Colour, "Vehicle colour", 50);
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CompleteVehicleOnboarding,
            WorkflowActor.Driver,
            cancellationToken);
    }

    public async Task<VehicleResponseDto> UpdateMediaAsync(
        Guid vehicleId,
        Guid requestingUserId,
        IReadOnlyDictionary<VehicleImageType, string> imageUrls,
        string? walkAroundVideoUrl,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);
        if (imageUrls.Count == 0 && string.IsNullOrWhiteSpace(walkAroundVideoUrl))
            throw new ValidationException("At least one vehicle media file is required.");

        var existingImages = await unitOfWork.VehicleImages
            .GetByVehicleIdAsync(vehicleId, cancellationToken);
        foreach (var item in imageUrls)
        {
            if (!Enum.IsDefined(item.Key) || string.IsNullOrWhiteSpace(item.Value))
                throw new ValidationException("Vehicle image data is invalid.");

            var existing = existingImages.FirstOrDefault(x => x.ImageType == item.Key);
            if (existing is null)
            {
                await unitOfWork.VehicleImages.CreateAsync(new VehicleImage
                {
                    VehicleId = vehicleId,
                    ImageType = item.Key,
                    ImageUrl = item.Value.Trim(),
                    IsPrimary = item.Key == VehicleImageType.FrontView
                }, cancellationToken);
            }
            else
            {
                existing.ImageUrl = item.Value.Trim();
                existing.IsPrimary = item.Key == VehicleImageType.FrontView;
                unitOfWork.VehicleImages.Update(existing);
            }
        }

        if (!string.IsNullOrWhiteSpace(walkAroundVideoUrl))
        {
            vehicle.WalkAroundVideoUrl = walkAroundVideoUrl.Trim();
            unitOfWork.Vehicles.Update(vehicle);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CompleteVehicleOnboarding,
            WorkflowActor.Driver,
            cancellationToken);
    }

    public async Task<VehicleResponseDto> UpdateCapacityExtrasAsync(
        Guid vehicleId,
        Guid requestingUserId,
        VehicleCapacityExtrasRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);
        ValidatePassengerSeatCount(request.PassengerSeatCount);
        if (request.LuggageCapacity.HasValue &&
            !Enum.IsDefined(request.LuggageCapacity.Value))
            throw new ValidationException("Luggage capacity is invalid.");

        var additionalDetails = NormalizeOptionalText(request.AdditionalDetails);
        if (additionalDetails?.Length > 1000)
            throw new ValidationException("Additional details cannot exceed 1000 characters.");

        var requestedAmenities = request.Amenities.Distinct().ToHashSet();
        if (requestedAmenities.Any(x => !Enum.IsDefined(x)))
            throw new ValidationException("One or more vehicle amenities are invalid.");

        var existingAmenities = await unitOfWork.VehicleAmenities
            .GetByVehicleIdAsync(vehicleId, cancellationToken);
        foreach (var existing in existingAmenities
                     .Where(x => !requestedAmenities.Contains(x.AmenityType)))
        {
            unitOfWork.VehicleAmenities.Delete(existing);
        }

        var existingTypes = existingAmenities.Select(x => x.AmenityType).ToHashSet();
        foreach (var amenityType in requestedAmenities.Where(x => !existingTypes.Contains(x)))
        {
            await unitOfWork.VehicleAmenities.CreateAsync(new VehicleAmenity
            {
                VehicleId = vehicleId,
                AmenityType = amenityType
            }, cancellationToken);
        }

        vehicle.PassengerSeatCount = request.PassengerSeatCount;
        vehicle.Capacity = request.PassengerSeatCount;
        if (request.LuggageCapacity.HasValue)
            vehicle.LuggageCapacity = request.LuggageCapacity.Value;
        vehicle.AdditionalDetails = additionalDetails;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CompleteVehicleOnboarding,
            WorkflowActor.Driver,
            cancellationToken);
    }

    public async Task<VehicleResponseDto> SubmitAsync(
        Guid vehicleId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);
        await ValidateCompletenessAsync(vehicle, cancellationToken);

        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        vehicle.IsActive = false;
        vehicle.RejectionReason = null;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.AwaitAdminApproval,
            WorkflowActor.Admin,
            cancellationToken);
    }

    public async Task DeleteAsync(Guid vehicleId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);

        unitOfWork.Vehicles.Delete(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<VehicleResponseDto> AddImagesAsync(
        Guid vehicleId, Guid requestingUserId, List<string> imageUrls, CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);

        if (imageUrls is null || imageUrls.Count == 0)
            throw new ValidationException("At least one image is required.");

        var existingImages = await unitOfWork.VehicleImages.GetByVehicleIdAsync(vehicleId, cancellationToken);
        var hasPrimary = existingImages.Any(i => i.IsPrimary);

        for (var i = 0; i < imageUrls.Count; i++)
        {
            await unitOfWork.VehicleImages.CreateAsync(new VehicleImage
            {
                VehicleId = vehicleId,
                ImageUrl = imageUrls[i].Trim(),
                IsPrimary = !hasPrimary && i == 0
            }, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CompleteVehicleOnboarding,
            WorkflowActor.Driver,
            cancellationToken);
    }

    public async Task DeleteImageAsync(Guid vehicleId, Guid imageId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);

        var images = await unitOfWork.VehicleImages.GetByVehicleIdAsync(vehicleId, cancellationToken);
        if (images.Count <= 1)
            throw new ValidationException("A vehicle must have at least one image — add a replacement before deleting the last one.");

        var image = images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new NotFoundException(nameof(VehicleImage), imageId);

        unitOfWork.VehicleImages.Delete(image);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<VehicleResponseDto> ActivateAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);

        if (vehicle.OnboardingStatus is not (
                VehicleOnboardingStatus.PendingReview or
                VehicleOnboardingStatus.Approved))
        {
            throw new ConflictException(
                "Only a vehicle pending review can be approved.");
        }

        var kyc = await unitOfWork.KycVerifications
            .GetByUserIdAsync(vehicle.UserId, cancellationToken);
        if (kyc?.Status != KycStatus.Approved)
            throw new ForbiddenException(
                "The vehicle owner must have approved KYC before vehicle activation.");

        if (vehicle.OnboardingStatus == VehicleOnboardingStatus.Approved)
        {
            vehicle.IsActive = true;
            unitOfWork.Vehicles.Update(vehicle);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildWorkflowResponseAsync(
                vehicle,
                WorkflowNextAction.CreateTrip,
                WorkflowActor.Driver,
                cancellationToken);
        }

        await ValidateCompletenessAsync(vehicle, cancellationToken);

        vehicle.OnboardingStatus = VehicleOnboardingStatus.Approved;
        vehicle.IsActive = true;
        vehicle.RejectionReason = null;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CreateTrip,
            WorkflowActor.Driver,
            cancellationToken);
    }

    public async Task<VehicleResponseDto> DeactivateAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);
        vehicle.IsActive = false;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildResponseAsync(vehicle, cancellationToken);
    }

    public async Task<VehicleResponseDto> RejectAsync(
        Guid vehicleId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(
            vehicleId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);

        if (vehicle.OnboardingStatus != VehicleOnboardingStatus.PendingReview)
            throw new ConflictException("Only a vehicle pending review can be rejected.");

        vehicle.OnboardingStatus = VehicleOnboardingStatus.Rejected;
        vehicle.IsActive = false;
        vehicle.RejectionReason = NormalizeRequiredText(
            reason,
            "Rejection reason",
            500);

        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CompleteVehicleOnboarding,
            WorkflowActor.Driver,
            cancellationToken);
    }

    private async Task<VehicleResponseDto> BuildResponseAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        var images = await unitOfWork.VehicleImages.GetByVehicleIdAsync(vehicle.Id, cancellationToken);
        var amenities = await unitOfWork.VehicleAmenities
            .GetByVehicleIdAsync(vehicle.Id, cancellationToken);
        return new VehicleResponseDto
        {
            Id = vehicle.Id,
            UserId = vehicle.UserId,
            LicensePlateNumber = vehicle.LicensePlateNumber,
            VehicleOwnerName = vehicle.VehicleOwnerName,
            RegistrationType = vehicle.RegistrationType,
            VehicleType = vehicle.VehicleType,
            Make = vehicle.Make,
            Model = vehicle.Model,
            ManufacturingYear = vehicle.ManufacturingYear,
            Colour = vehicle.Colour,
            WalkAroundVideoUrl = vehicle.WalkAroundVideoUrl,
            PassengerSeatCount = vehicle.PassengerSeatCount,
            LuggageCapacity = vehicle.LuggageCapacity,
            Amenities = amenities.Select(x => x.AmenityType).Order().ToList(),
            AdditionalDetails = vehicle.AdditionalDetails,
            OnboardingStatus = vehicle.OnboardingStatus,
            RejectionReason = vehicle.RejectionReason,
            Capacity = vehicle.Capacity,
            IsActive = vehicle.IsActive,
            ImageUrls = images.OrderByDescending(i => i.IsPrimary).Select(i => i.ImageUrl).ToList(),
            Images = images
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.ImageType)
                .Select(i => new VehicleImageResponseDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    ImageType = i.ImageType,
                    IsPrimary = i.IsPrimary
                })
                .ToList()
        };
    }

    private async Task<VehicleResponseDto> BuildWorkflowResponseAsync(
        Vehicle vehicle,
        WorkflowNextAction nextAction,
        WorkflowActor requiredActor,
        CancellationToken cancellationToken)
    {
        var response = await BuildResponseAsync(
            vehicle,
            cancellationToken);
        response.WorkflowStatus = vehicle.OnboardingStatus;
        response.NextAction = nextAction;
        response.RequiredActor = requiredActor;
        return response;
    }

    private async Task<Vehicle> GetEditableOwnedVehicleAsync(
        Guid vehicleId,
        Guid requestingUserId,
        CancellationToken cancellationToken)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);
        if (vehicle.UserId != requestingUserId)
            throw new ForbiddenException("You do not have access to this vehicle.");
        if (vehicle.OnboardingStatus is not (
                VehicleOnboardingStatus.Draft or
                VehicleOnboardingStatus.Rejected))
        {
            throw new ConflictException(
                $"A vehicle in {vehicle.OnboardingStatus} status cannot be edited by the driver.");
        }
        return vehicle;
    }

    private async Task ValidateCompletenessAsync(
        Vehicle vehicle,
        CancellationToken cancellationToken)
    {
        var documents = await unitOfWork.VehicleDocuments
            .GetByVehicleIdAsync(vehicle.Id, cancellationToken);
        var images = await unitOfWork.VehicleImages
            .GetByVehicleIdAsync(vehicle.Id, cancellationToken);
        var missingRequirements = new List<string>();

        AddMissingText(
            missingRequirements,
            vehicle.VehicleOwnerName,
            "vehicle owner name");
        if (vehicle.RegistrationType is null ||
            !Enum.IsDefined(vehicle.RegistrationType.Value))
        {
            missingRequirements.Add("registration type");
        }
        AddMissingText(
            missingRequirements,
            vehicle.VehicleType,
            "vehicle type");
        AddMissingText(missingRequirements, vehicle.Make, "make");
        AddMissingText(missingRequirements, vehicle.Model, "model");
        if (!vehicle.ManufacturingYear.HasValue ||
            !IsValidManufacturingYear(vehicle.ManufacturingYear.Value))
        {
            missingRequirements.Add("valid manufacturing year");
        }
        AddMissingText(missingRequirements, vehicle.Colour, "colour");

        if (!documents.Any(document =>
                document.DocumentType ==
                VehicleDocumentType.VehicleRegistration))
        {
            missingRequirements.Add("vehicle registration document");
        }

        foreach (var imageType in Enum.GetValues<VehicleImageType>())
        {
            if (images.All(image => image.ImageType != imageType))
                missingRequirements.Add($"{FormatImageType(imageType)} photograph");
        }

        if (!vehicle.PassengerSeatCount.HasValue ||
            !AllowedPassengerSeatCounts.Contains(
                vehicle.PassengerSeatCount.Value))
        {
            missingRequirements.Add("valid passenger capacity");
        }

        if (missingRequirements.Count > 0)
        {
            throw new ValidationException(
                $"Vehicle onboarding is incomplete. Missing: {string.Join(", ", missingRequirements)}.");
        }
    }

    private static void ValidateVehicleDetails(
        VehicleDetailsRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = NormalizeRequiredText(
            request.VehicleOwnerName, "Vehicle owner name", 200);
        if (!Enum.IsDefined(request.RegistrationType))
            throw new ValidationException(
                "Vehicle registration type is invalid.");
        _ = NormalizeRequiredText(
            request.VehicleType, "Vehicle type", 100);
        _ = NormalizeRequiredText(
            request.Make, "Vehicle make", 100);
        _ = NormalizeRequiredText(
            request.Model, "Vehicle model", 100);
        if (!IsValidManufacturingYear(request.ManufacturingYear))
        {
            throw new ValidationException(
                $"Manufacturing year must be between 1900 and {DateTime.UtcNow.Year + 1}.");
        }
        _ = NormalizeRequiredText(
            request.Colour, "Vehicle colour", 50);
    }

    private static void ValidatePassengerSeatCount(int passengerSeatCount)
    {
        if (!AllowedPassengerSeatCounts.Contains(passengerSeatCount))
            throw new ValidationException(
                "Passenger seat count must be 2, 4, 5, 6, or 7.");
    }

    private static bool IsValidManufacturingYear(int year) =>
        year is >= 1900 && year <= DateTime.UtcNow.Year + 1;

    private static void AddMissingText(
        ICollection<string> missingRequirements,
        string? value,
        string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            missingRequirements.Add(name);
    }

    private static string FormatImageType(VehicleImageType imageType) =>
        imageType switch
        {
            VehicleImageType.FrontView => "front view",
            VehicleImageType.RearView => "rear view",
            VehicleImageType.SideProfile => "side profile",
            VehicleImageType.Interior => "interior",
            _ => imageType.ToString()
        };

    private static string NormalizeRequiredText(
        string? value,
        string name,
        int maximumLength)
    {
        var normalized = NormalizeOptionalText(value);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ValidationException($"{name} is required.");
        if (normalized.Length > maximumLength)
        {
            throw new ValidationException(
                $"{name} cannot exceed {maximumLength} characters.");
        }
        return normalized;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return string.Join(' ', value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
