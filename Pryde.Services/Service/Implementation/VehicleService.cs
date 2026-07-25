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

        return await BuildResponseAsync(vehicle, cancellationToken);
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

        return await BuildResponseAsync(vehicle, cancellationToken);
    }

    public async Task<VehicleResponseDto> UpdateDetailsAsync(
        Guid vehicleId,
        Guid requestingUserId,
        VehicleDetailsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);
        var ownerName = NormalizeOptionalText(request.VehicleOwnerName);
        if (string.IsNullOrWhiteSpace(ownerName))
            throw new ValidationException("Vehicle owner name is required.");
        if (ownerName.Length > 200)
            throw new ValidationException("Vehicle owner name cannot exceed 200 characters.");
        if (!Enum.IsDefined(request.RegistrationType))
            throw new ValidationException("Vehicle registration type is invalid.");

        vehicle.VehicleOwnerName = ownerName;
        vehicle.RegistrationType = request.RegistrationType;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildResponseAsync(vehicle, cancellationToken);
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
        return await BuildResponseAsync(vehicle, cancellationToken);
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
        if (!Enum.IsDefined(request.LuggageCapacity))
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
        vehicle.LuggageCapacity = request.LuggageCapacity;
        vehicle.AdditionalDetails = additionalDetails;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildResponseAsync(vehicle, cancellationToken);
    }

    public async Task<VehicleResponseDto> SubmitAsync(
        Guid vehicleId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);
        var documents = await unitOfWork.VehicleDocuments
            .GetByVehicleIdAsync(vehicleId, cancellationToken);
        var images = await unitOfWork.VehicleImages
            .GetByVehicleIdAsync(vehicleId, cancellationToken);
        var requiredImageTypes = Enum.GetValues<VehicleImageType>();

        if (string.IsNullOrWhiteSpace(vehicle.VehicleOwnerName) ||
            vehicle.RegistrationType is null ||
            !documents.Any(x => x.DocumentType == VehicleDocumentType.VehicleRegistration) ||
            requiredImageTypes.Any(type => images.All(x => x.ImageType != type)) ||
            vehicle.PassengerSeatCount is null ||
            !AllowedPassengerSeatCounts.Contains(vehicle.PassengerSeatCount.Value) ||
            vehicle.LuggageCapacity is null)
        {
            throw new ValidationException(
                "Vehicle onboarding is incomplete. Complete details, registration document, required media, and capacity before submission.");
        }

        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        vehicle.IsActive = false;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildResponseAsync(vehicle, cancellationToken);
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
        return await BuildResponseAsync(vehicle, cancellationToken);
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
            return await BuildResponseAsync(vehicle, cancellationToken);
        }

        if (vehicle.OnboardingStatus != VehicleOnboardingStatus.PendingReview)
            throw new ConflictException("Only a vehicle pending review can be approved.");

        vehicle.OnboardingStatus = VehicleOnboardingStatus.Approved;
        vehicle.IsActive = true;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildResponseAsync(vehicle, cancellationToken);
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
            WalkAroundVideoUrl = vehicle.WalkAroundVideoUrl,
            PassengerSeatCount = vehicle.PassengerSeatCount,
            LuggageCapacity = vehicle.LuggageCapacity,
            Amenities = amenities.Select(x => x.AmenityType).Order().ToList(),
            AdditionalDetails = vehicle.AdditionalDetails,
            OnboardingStatus = vehicle.OnboardingStatus,
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

    private async Task<Vehicle> GetEditableOwnedVehicleAsync(
        Guid vehicleId,
        Guid requestingUserId,
        CancellationToken cancellationToken)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);
        if (vehicle.UserId != requestingUserId)
            throw new ForbiddenException("You do not have access to this vehicle.");
        if (vehicle.OnboardingStatus == VehicleOnboardingStatus.Approved)
            throw new ConflictException("An approved vehicle cannot be edited by the driver.");
        return vehicle;
    }

    private static void ValidatePassengerSeatCount(int passengerSeatCount)
    {
        if (!AllowedPassengerSeatCounts.Contains(passengerSeatCount))
            throw new ValidationException(
                "Passenger seat count must be 2, 4, 5, 6, or 7.");
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
