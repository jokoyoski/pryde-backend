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
    private const string DojahProviderName = "Dojah";
    private const string DojahCompletedStatus = "Completed";

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
        {
            throw new ConflictException("This KYC request has already been finalized.");
        }

        await ValidateCompletedDojahAttemptAsync(
            kyc,
            cancellationToken);

        var requiresDriverOnboarding = await RequiresDriverOnboardingAsync(
            userId,
            cancellationToken);

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

    private async Task ValidateCompletedDojahAttemptAsync(
        KycVerification kyc,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                kyc.ProviderName,
                DojahProviderName,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                kyc.ProviderStatus,
                DojahCompletedStatus,
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(kyc.ProviderReference) ||
            string.IsNullOrWhiteSpace(kyc.DojahReference) ||
            !kyc.LastProviderUpdatedAt.HasValue)
        {
            throw new ConflictException(
                "Only a successfully completed Dojah verification can be approved.");
        }

        if (kyc.ProviderReference.Equals(
                kyc.DojahReference,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                "The Dojah verification references do not match the active verification.");
        }

        var providerReferenceOwner = await unitOfWork.KycVerifications
            .GetByProviderReferenceAsync(
                kyc.ProviderReference,
                cancellationToken);
        var dojahReferenceOwner = await unitOfWork.KycVerifications
            .GetByDojahReferenceAsync(
                kyc.DojahReference,
                cancellationToken);

        if (providerReferenceOwner?.Id != kyc.Id ||
            dojahReferenceOwner?.Id != kyc.Id)
        {
            throw new ConflictException(
                "The Dojah verification references do not match the active verification.");
        }
    }

    private async Task<bool> RequiresDriverOnboardingAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var userRoles = await unitOfWork.UserRoles.GetByUserIdAsync(
            userId,
            cancellationToken);

        return userRoles.Any(role =>
            role.Role.Name == RoleNames.Driver);
    }

    public async Task<KycVerificationResponseDto> RejectAsync(
        Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        var sanitizedReason = SanitizeReason(reason);
        var kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(KycVerification), userId);

        if (kyc.Status is KycStatus.Approved or KycStatus.Rejected)
        {
            throw new ConflictException("This KYC request has already been finalized.");
        }

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
