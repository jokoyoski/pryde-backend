using Mapster;
using Pryde.Domain.Common.Exceptions;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;
namespace Pryde.Services.Service.Implementation;
public class VehicleDocumentService(IUnitOfWork unitOfWork) : IVehicleDocumentService
{
    public async Task<VehicleDocumentResponseDto> UploadAsync(Guid vehicleId, Guid requestingUserId, VehicleDocumentType documentType, DateTime expiryDate, string documentUrl, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);

        if (vehicle.UserId != requestingUserId)
            throw new ForbiddenException("You do not have access to this vehicle.");

        if (string.IsNullOrWhiteSpace(documentUrl))
            throw new ValidationException("A document file is required.");
        if (expiryDate <= DateTime.UtcNow)
            throw new ValidationException("Expiry date must be in the future.");

        var document = new VehicleDocument
        {
            VehicleId = vehicleId,
            DocumentType = documentType,
            DocumentUrl = documentUrl.Trim(),
            ExpiryDate = expiryDate
        };

        await unitOfWork.VehicleDocuments.CreateAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return document.Adapt<VehicleDocumentResponseDto>();
    }

    public async Task<IReadOnlyList<VehicleDocumentResponseDto>> GetByVehicleIdAsync(Guid vehicleId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);

        if (vehicle.UserId != requestingUserId)
            throw new ForbiddenException("You do not have access to this vehicle.");

        var documents = await unitOfWork.VehicleDocuments.GetByVehicleIdAsync(vehicleId, cancellationToken);
        return documents.Adapt<List<VehicleDocumentResponseDto>>();
    }

    public async Task<IReadOnlyList<VehicleDocumentResponseDto>> GetExpiringAsync(int withinDays, CancellationToken cancellationToken = default)
    {
        var threshold = DateTime.UtcNow.AddDays(withinDays);
        var documents = await unitOfWork.VehicleDocuments.GetExpiringBeforeAsync(threshold, cancellationToken);
        return documents.Adapt<List<VehicleDocumentResponseDto>>();
    }

    public async Task DeleteAsync(Guid documentId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var document = await unitOfWork.VehicleDocuments.GetByIdAsync(documentId, cancellationToken)
            ?? throw new NotFoundException(nameof(VehicleDocument), documentId);

        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(document.VehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), document.VehicleId);

        if (vehicle.UserId != requestingUserId)
            throw new ForbiddenException("You do not have access to this document.");

        unitOfWork.VehicleDocuments.Delete(document);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}