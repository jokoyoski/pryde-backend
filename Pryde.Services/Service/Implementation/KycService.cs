using Mapster;
using Microsoft.AspNetCore.Http;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;
using Pryde.Services.Storage.Enums;
using Pryde.Services.Storage.Interface;

namespace Pryde.Services.Service.Implementation;

public class KycService(
    IUnitOfWork unitOfWork,
    IFileStorageService fileStorageService) : IKycService
{
    public async Task<KycVerificationResponseDto> UploadDocumentsAsync(
        Guid userId,
        KycDocumentUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateDocumentRequest(request);

        await ValidateDriverDocumentUploadAsync(
            userId,
            request,
            cancellationToken);

        var kyc = await GetOrCreateKycAsync(
            userId,
            cancellationToken);

        kyc.BiometricVerificationUrl = await UploadOptionalAsync(
            request.BiometricVerification,
            FileCategory.KycBiometric,
            userId,
            kyc.BiometricVerificationUrl,
            cancellationToken);

        kyc.DriverLicenseUrl = await UploadOptionalAsync(
            request.DriverLicense,
            FileCategory.KycDriverLicense,
            userId,
            kyc.DriverLicenseUrl,
            cancellationToken);

        kyc.SecondaryIdentificationUrl = await UploadOptionalAsync(
            request.SecondaryIdentification,
            FileCategory.KycSecondaryId,
            userId,
            kyc.SecondaryIdentificationUrl,
            cancellationToken);

        kyc.Status = KycStatus.Pending;
        kyc.VerifiedAt = null;

        unitOfWork.KycVerifications.Update(kyc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return kyc.Adapt<KycVerificationResponseDto>();
    }

    public async Task<KycVerificationResponseDto> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(
            userId,
            cancellationToken);

        if (kyc is null)
        { 
            throw new NotFoundException( nameof(KycVerification), userId);
        }

        return kyc.Adapt<KycVerificationResponseDto>();
    }

    private static void ValidateDocumentRequest(KycDocumentUploadRequest request)
    {
        if (request.BiometricVerification is null &&
            request.DriverLicense is null &&
            request.SecondaryIdentification is null)
        {
            throw new ValidationException(
                "At least one KYC document is required.");
        }
    }

    private async Task ValidateDriverDocumentUploadAsync(
        Guid userId,
        KycDocumentUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DriverLicense is null &&
            request.SecondaryIdentification is null)
        {
            return;
        }

        var userRoles = await unitOfWork.UserRoles.GetByUserIdAsync(
            userId,
            cancellationToken);

        var isDriver = userRoles.Any(role =>
            role.Role.Name == RoleType.Driver.ToString());

        if (!isDriver)
        {
            throw new ForbiddenException(
                "Driver's license and secondary identification can only be uploaded by users with the Driver role.");
        }
    }

    private async Task<KycVerification> GetOrCreateKycAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(
            userId,
            cancellationToken);

        if (kyc is not null)
            return kyc;

        kyc = new KycVerification
        {
            UserId = userId,
            Status = KycStatus.Pending
        };

        await unitOfWork.KycVerifications.CreateAsync(
            kyc,
            cancellationToken);

        return kyc;
    }

    private async Task<string?> UploadOptionalAsync(
        IFormFile? file,
        FileCategory category,
        Guid userId,
        string? existingUrl,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return existingUrl;

        if (file.Length == 0)
            throw new ValidationException("Uploaded file cannot be empty.");

        await using var stream = file.OpenReadStream();

        var upload = await fileStorageService.UploadAsync(
            stream,
            file.FileName,
            file.ContentType,
            category,
            userId,
            cancellationToken);

        return upload.PublicUrl;
    }
}