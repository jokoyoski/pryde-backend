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
    public async Task<VehicleDocumentResponseDto> UploadAsync(Guid vehicleId, Guid requestingUserId, VehicleDocumentType documentType, DateTime? expiryDate, string documentUrl, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);

        if (vehicle.UserId != requestingUserId)
            throw new ForbiddenException("You do not have access to this vehicle.");
        EnsureVehicleCanBeEdited(vehicle);

        if (!Enum.IsDefined(documentType))
            throw new ValidationException("Vehicle document type is invalid.");
        if (string.IsNullOrWhiteSpace(documentUrl))
            throw new ValidationException("A document file is required.");
        if (RequiresExpiry(documentType) && !expiryDate.HasValue)
        {
            throw new ValidationException(
                $"Expiry date is required for {documentType}.");
        }
        if (expiryDate.HasValue &&
            expiryDate.Value <= DateTime.UtcNow)
            throw new ValidationException("Expiry date must be in the future.");

        var documents = await unitOfWork.VehicleDocuments
            .GetByVehicleIdAsync(vehicleId, cancellationToken);
        var document = documents.FirstOrDefault(x => x.DocumentType == documentType);
        var isNew = document is null;
        if (document is null)
        {
            document = new VehicleDocument
            {
                VehicleId = vehicleId,
                DocumentType = documentType
            };
            await unitOfWork.VehicleDocuments.CreateAsync(document, cancellationToken);
        }

        document.DocumentUrl = documentUrl.Trim();
        document.ExpiryDate = expiryDate;
        document.ReviewStatus = VehicleDocumentReviewStatus.Pending;
        document.ReviewedBy = null;
        document.ReviewedAt = null;
        document.RejectionReason = null;
        if (!isNew)
            unitOfWork.VehicleDocuments.Update(document);
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

    public async Task<VehicleDocumentResponseDto> GetForAdminAsync(
        Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await unitOfWork.VehicleDocuments.GetByIdAsync(documentId, cancellationToken)
            ?? throw new NotFoundException(nameof(VehicleDocument), documentId);
        return document.Adapt<VehicleDocumentResponseDto>();
    }

    public async Task<VehicleDocumentResponseDto> ApproveAsync(
        Guid documentId, Guid reviewedBy, CancellationToken cancellationToken = default)
    {
        var document = await GetPendingDocumentAsync(documentId, cancellationToken);
        document.ReviewStatus = VehicleDocumentReviewStatus.Approved;
        document.ReviewedBy = reviewedBy;
        document.ReviewedAt = DateTime.UtcNow;
        document.RejectionReason = null;
        unitOfWork.VehicleDocuments.Update(document);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return document.Adapt<VehicleDocumentResponseDto>();
    }

    public async Task<VehicleDocumentResponseDto> RejectAsync(
        Guid documentId, Guid reviewedBy, string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationException("A rejection reason is required.");
        var document = await GetPendingDocumentAsync(documentId, cancellationToken);
        document.ReviewStatus = VehicleDocumentReviewStatus.Rejected;
        document.ReviewedBy = reviewedBy;
        document.ReviewedAt = DateTime.UtcNow;
        document.RejectionReason = string.Join(' ', reason.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (document.RejectionReason.Length > 500)
            document.RejectionReason = document.RejectionReason[..500];
        unitOfWork.VehicleDocuments.Update(document);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return document.Adapt<VehicleDocumentResponseDto>();
    }

    public async Task DeleteAsync(Guid documentId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var document = await unitOfWork.VehicleDocuments.GetByIdAsync(documentId, cancellationToken)
            ?? throw new NotFoundException(nameof(VehicleDocument), documentId);

        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(document.VehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), document.VehicleId);

        if (vehicle.UserId != requestingUserId)
            throw new ForbiddenException("You do not have access to this document.");
        EnsureVehicleCanBeEdited(vehicle);

        unitOfWork.VehicleDocuments.Delete(document);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<VehicleDocument> GetPendingDocumentAsync(
        Guid documentId, CancellationToken cancellationToken)
    {
        var document = await unitOfWork.VehicleDocuments.GetByIdAsync(documentId, cancellationToken)
            ?? throw new NotFoundException(nameof(VehicleDocument), documentId);
        if (document.ReviewStatus != VehicleDocumentReviewStatus.Pending)
            throw new ConflictException("This vehicle document has already been finalized.");
        return document;
    }

    private static void EnsureVehicleCanBeEdited(Vehicle vehicle)
    {
        if (vehicle.OnboardingStatus is not (
                VehicleOnboardingStatus.Draft or
                VehicleOnboardingStatus.Rejected))
        {
            throw new ConflictException(
                $"A vehicle in {vehicle.OnboardingStatus} status cannot be edited by the driver.");
        }
    }

    private static bool RequiresExpiry(
        VehicleDocumentType documentType) =>
        documentType is
            VehicleDocumentType.Insurance or
            VehicleDocumentType.RoadworthinessCertificate or
            VehicleDocumentType.DriversLicense;
}
