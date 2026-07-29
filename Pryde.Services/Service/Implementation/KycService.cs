using Mapster;
using Microsoft.AspNetCore.Http;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Constants;
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

        if (kyc.Status == KycStatus.Approved)
        {
            throw new ConflictException(
                "Approved KYC documents cannot be modified.");
        }

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
        kyc.RejectionReason = null;
        kyc.ProviderReference = null;
        kyc.DojahReference = null;
        kyc.ProviderStatus = null;
        kyc.LastProviderUpdatedAt = null;

        unitOfWork.KycVerifications.Update(kyc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return WorkflowResponse(
            kyc,
            WorkflowNextAction.CompleteKyc,
            WorkflowActor.User);
    }

    public async Task<KycVerificationResponseDto> SubmitAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(
            userId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(KycVerification), userId);

        if (kyc.Status == KycStatus.Approved)
        {
            throw new ConflictException(
                "Approved KYC cannot be resubmitted.");
        }

        if (kyc.Status == KycStatus.Submitted)
        {
            throw new ConflictException(
                "KYC has already been submitted.");
        }

        await ValidateCompletenessAsync(
            userId,
            kyc,
            cancellationToken);

        kyc.Status = KycStatus.Submitted;
        kyc.VerifiedAt = null;
        kyc.RejectionReason = null;

        unitOfWork.KycVerifications.Update(kyc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return WorkflowResponse(
            kyc,
            WorkflowNextAction.AwaitAdminApproval,
            WorkflowActor.Admin);
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
            role.Role.Name == RoleNames.Driver);

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
    public async Task<KycVerificationResponseDto> ApproveAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(KycVerification), userId);

        if (kyc.Status is KycStatus.Approved or KycStatus.Rejected)
            throw new ConflictException("This KYC request has already been finalized.");

        var requiresDriverOnboarding = await ValidateCompletenessAsync(
            userId,
            kyc,
            cancellationToken);

        if (kyc.Status != KycStatus.Submitted)
        {
            throw new ConflictException(
                "KYC must be submitted before approval.");
        }

        kyc.Status = KycStatus.Approved;
        kyc.VerifiedAt = DateTime.UtcNow;
        kyc.RejectionReason = null;

        unitOfWork.KycVerifications.Update(kyc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return WorkflowResponse(
            kyc,
            requiresDriverOnboarding
                ? WorkflowNextAction.CompleteVehicleOnboarding
                : WorkflowNextAction.None,
            requiresDriverOnboarding
                ? WorkflowActor.Driver
                : WorkflowActor.None);
    }

    private async Task<bool> ValidateCompletenessAsync(
        Guid userId,
        KycVerification kyc,
        CancellationToken cancellationToken)
    {
        var missingDocuments = new List<string>();

        if (string.IsNullOrWhiteSpace(kyc.BiometricVerificationUrl))
        {
            missingDocuments.Add("biometric verification");
        }

        var userRoles = await unitOfWork.UserRoles.GetByUserIdAsync(
            userId,
            cancellationToken);
        var requiresDriverDocuments = userRoles.Any(role =>
            role.Role.Name == RoleNames.Driver);

        if (requiresDriverDocuments &&
            string.IsNullOrWhiteSpace(kyc.DriverLicenseUrl))
        {
            missingDocuments.Add("driver's license");
        }

        if (requiresDriverDocuments &&
            string.IsNullOrWhiteSpace(kyc.SecondaryIdentificationUrl))
        {
            missingDocuments.Add("secondary identification");
        }

        if (missingDocuments.Count > 0)
        {
            throw new ValidationException(
                $"Missing required KYC documents: {string.Join(", ", missingDocuments)}.");
        }

        return requiresDriverDocuments;
    }

    public async Task<KycVerificationResponseDto> RejectAsync(
        Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        var sanitizedReason = SanitizeReason(reason);
        var kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(KycVerification), userId);

        if (kyc.Status is KycStatus.Approved or KycStatus.Rejected)
            throw new ConflictException("This KYC request has already been finalized.");

        kyc.Status = KycStatus.Rejected;
        kyc.VerifiedAt = null;
        kyc.RejectionReason = sanitizedReason;

        unitOfWork.KycVerifications.Update(kyc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return WorkflowResponse(
            kyc,
            WorkflowNextAction.CompleteKyc,
            WorkflowActor.User);
    }

    private static KycVerificationResponseDto WorkflowResponse(
        KycVerification kyc,
        WorkflowNextAction nextAction,
        WorkflowActor requiredActor)
    {
        var response = kyc.Adapt<KycVerificationResponseDto>();
        response.NextAction = nextAction;
        response.RequiredActor = requiredActor;
        return response;
    }

    private static string SanitizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException("A rejection reason is required.");
        }

        var sanitized = string.Join(' ', reason.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return sanitized.Length <= 500 ? sanitized : sanitized[..500];
    }
}
