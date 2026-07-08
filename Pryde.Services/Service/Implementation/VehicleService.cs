using Mapster;
using Pryde.Domain.Common.Exceptions;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;
namespace Pryde.Services.Service.Implementation;
public class VehicleService(IUnitOfWork unitOfWork) : IVehicleService
{
    public async Task<VehicleResponseDto> CreateAsync( Guid driverId,string licensePlateNumber,
    int capacity,string vehicleImageUrl, CancellationToken cancellationToken = default)
{
    licensePlateNumber = licensePlateNumber?.Trim().ToUpperInvariant();
    vehicleImageUrl = vehicleImageUrl?.Trim();

    if (string.IsNullOrWhiteSpace(licensePlateNumber))
        throw new ValidationException("License plate number is required.");

    if (string.IsNullOrWhiteSpace(vehicleImageUrl))
        throw new ValidationException("Vehicle image is required.");

    if (capacity <= 0)
        throw new ValidationException("Capacity must be greater than zero.");

    var userRoles = await unitOfWork.UserRoles.GetByUserIdAsync(driverId, cancellationToken);

    var isDriver = userRoles.Any(ur => ur.Role.Name == RoleType.Driver.ToString());

    if (!isDriver)
        throw new ForbiddenException("Only users with the Driver role can register a vehicle.");

    var kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(driverId, cancellationToken);

    if (kyc is null || kyc.Status != KycStatus.Approved)
        throw new ForbiddenException("Your KYC verification must be approved before registering a vehicle.");

    if (await unitOfWork.Vehicles.ExistsAsync(licensePlateNumber, cancellationToken))
        throw new ConflictException("A vehicle with this license plate is already registered.");

    var vehicle = new Vehicle
    {
        UserId = driverId,
        LicensePlateNumber = licensePlateNumber,
        VehicleImageUrl = vehicleImageUrl,
        Capacity = capacity,
        IsActive = false
    };

    await unitOfWork.Vehicles.CreateAsync(vehicle, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return vehicle.Adapt<VehicleResponseDto>();
}

    public async Task<VehicleResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), id);
        return vehicle.Adapt<VehicleResponseDto>();
    }

    public async Task<IReadOnlyList<VehicleResponseDto>> GetMyVehiclesAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        var vehicles = await unitOfWork.Vehicles.GetByUserIdAsync(driverId, cancellationToken);
        return vehicles.Adapt<List<VehicleResponseDto>>();
    }

    public async Task<VehicleResponseDto> UpdateAsync(Guid vehicleId, Guid requestingUserId, int capacity, string vehicleImageUrl, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);

        if (vehicle.UserId != requestingUserId)
            throw new ForbiddenException("You do not have access to this vehicle.");

        if (capacity <= 0)
            throw new ValidationException("Capacity must be greater than zero.");
        if (string.IsNullOrWhiteSpace(vehicleImageUrl))
            throw new ValidationException("Vehicle image is required.");

        vehicle.VehicleImageUrl = vehicleImageUrl.Trim();
        vehicle.Capacity = capacity;

        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return vehicle.Adapt<VehicleResponseDto>();
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

    public async Task<VehicleResponseDto> ActivateAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);
        vehicle.IsActive = true;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return vehicle.Adapt<VehicleResponseDto>();
    }

    public async Task<VehicleResponseDto> DeactivateAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);
        vehicle.IsActive = false;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return vehicle.Adapt<VehicleResponseDto>();
    }
}