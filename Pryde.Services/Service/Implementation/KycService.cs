using Mapster;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class KycService(
    IUnitOfWork unitOfWork,
    INotificationService notificationService) : IKycService
{
    public KycService(IUnitOfWork unitOfWork)
        : this(
            unitOfWork,
            new NotificationService(unitOfWork))
    {
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

        var response = WorkflowResponse(
            kyc,
            requiresDriverOnboarding
                ? WorkflowNextAction.CompleteVehicleOnboarding
                : WorkflowNextAction.None,
            requiresDriverOnboarding
                ? WorkflowActor.Driver
                : WorkflowActor.None);
        await notificationService.TryCreateAsync(
            NewNotification(
                userId,
                NotificationType.KycApproved,
                "KYC approved",
                "Your identity verification was approved.",
                kyc.Id,
                $"kyc-approved:{kyc.Id}"),
            cancellationToken);
        return response;
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

        var response = WorkflowResponse(
            kyc,
            WorkflowNextAction.CompleteKyc,
            WorkflowActor.User);
        await notificationService.TryCreateAsync(
            NewNotification(
                userId,
                NotificationType.KycRejected,
                "KYC rejected",
                $"Your identity verification was rejected: {sanitizedReason}",
                kyc.Id,
                $"kyc-rejected:{kyc.Id}"),
            cancellationToken);
        return response;
    }

    private static CreateNotificationRequest NewNotification(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        Guid kycId,
        string deduplicationKey)
    {
        return new CreateNotificationRequest
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            RelatedEntityId = kycId,
            RelatedEntityType = nameof(KycVerification),
            DeduplicationKey = deduplicationKey
        };
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
