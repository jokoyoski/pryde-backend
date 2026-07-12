using Pryde.Domain.Common.Exceptions;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class VehicleService(IUnitOfWork unitOfWork) : IVehicleService
{
    public async Task<VehicleResponseDto> CreateAsync(
        Guid driverId, string licensePlateNumber, int capacity,
        List<string> imageUrls, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(licensePlateNumber))
            throw new ValidationException("License plate number is required.");
        if (capacity <= 0)
            throw new ValidationException("Capacity must be greater than zero.");
        if (imageUrls is null || imageUrls.Count == 0)
            throw new ValidationException("At least one vehicle image is required.");

        var userRoles = await unitOfWork.UserRoles.GetByUserIdAsync(driverId, cancellationToken);
        var isDriver = userRoles.Any(ur => ur.Role.Name == RoleType.Driver.ToString());
        if (!isDriver)
            throw new ForbiddenException("Only users with the Driver role can register a vehicle.");

        var kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(driverId, cancellationToken);
        if (kyc is null || kyc.Status != KycStatus.Approved)
            throw new ForbiddenException("Your KYC verification must be approved before registering a vehicle.");

        var plate = licensePlateNumber.Trim();
        if (await unitOfWork.Vehicles.ExistsAsync(plate, cancellationToken))
            throw new ConflictException("A vehicle with this license plate is already registered.");

        var vehicle = new Vehicle
        {
            UserId = driverId,
            LicensePlateNumber = plate,
            Capacity = capacity,
            IsActive = false
        };

        await unitOfWork.Vehicles.CreateAsync(vehicle, cancellationToken);

        for (var i = 0; i < imageUrls.Count; i++)
        {
            await unitOfWork.VehicleImages.CreateAsync(new VehicleImage
            {
                VehicleId = vehicle.Id,
                ImageUrl = imageUrls[i].Trim(),
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
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);

        if (vehicle.UserId != requestingUserId)
            throw new ForbiddenException("You do not have access to this vehicle.");

        if (capacity <= 0)
            throw new ValidationException("Capacity must be greater than zero.");

        vehicle.Capacity = capacity;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await BuildResponseAsync(vehicle, cancellationToken);
    }

    public async Task DeleteAsync(Guid vehicleId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);

        if (vehicle.UserId != requestingUserId)
            throw new ForbiddenException("You do not have access to this vehicle.");

        unitOfWork.Vehicles.Delete(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<VehicleResponseDto> AddImagesAsync(
        Guid vehicleId, Guid requestingUserId, List<string> imageUrls, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);

        if (vehicle.UserId != requestingUserId)
            throw new ForbiddenException("You do not have access to this vehicle.");

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
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);

        if (vehicle.UserId != requestingUserId)
            throw new ForbiddenException("You do not have access to this vehicle.");

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
        return new VehicleResponseDto
        {
            Id = vehicle.Id,
            UserId = vehicle.UserId,
            LicensePlateNumber = vehicle.LicensePlateNumber,
            Capacity = vehicle.Capacity,
            IsActive = vehicle.IsActive,
            ImageUrls = images.OrderByDescending(i => i.IsPrimary).Select(i => i.ImageUrl).ToList()
        };
    }
}