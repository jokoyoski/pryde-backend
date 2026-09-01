using Mapster;
using Microsoft.Extensions.Options;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;
using Pryde.Services.Providers.Kyc;
using Pryde.Services.Settings;

namespace Pryde.Services.Service.Implementation;

public class KycService(
    IUnitOfWork unitOfWork,
    INotificationService notificationService,
    IOptions<KycSettings>? kycOptions = null,
    ISmileIdKycService? smileIdKycService = null) : IKycService
{
    private const string DojahProviderName = "Dojah";
    private const string DojahCompletedStatus = "Completed";
    private readonly KycSettings _kycSettings =
        kycOptions?.Value ?? new KycSettings();

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

        if (smileIdKycService is not null &&
            string.Equals(
                kyc.ProviderName,
                SmileIdKycProvider.ProviderName,
                StringComparison.OrdinalIgnoreCase) &&
            kyc.Status is KycStatus.Pending or KycStatus.Submitted)
        {
            await smileIdKycService.ReconcilePendingAsync(userId, cancellationToken);
            unitOfWork.ClearTracking();
            kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(
                userId,
                cancellationToken)
                ?? throw new NotFoundException(nameof(KycVerification), userId);
        }

        var response = kyc.Adapt<KycVerificationResponseDto>();
        response.AttemptAllowance = await KycAttemptAllowanceCalculator.GetAsync(
            unitOfWork,
            kyc.Id,
            _kycSettings,
            DateTime.UtcNow,
            cancellationToken);
        if (!string.Equals(kyc.ProviderName, "SmileId", StringComparison.OrdinalIgnoreCase))
        {
            response.CanRetry = kyc.Status == KycStatus.Rejected &&
                                response.AttemptAllowance.CanAttempt;
            return response;
        }

        unitOfWork.ClearTracking();
        return await unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
        {
            var lockedKyc = await unitOfWork.KycVerifications
                .GetByIdForUpdateAsync(kyc.Id, transactionToken)
                ?? throw new NotFoundException(nameof(KycVerification), kyc.Id);
            var lockedResponse = lockedKyc.Adapt<KycVerificationResponseDto>();
            if (!string.Equals(
                    lockedKyc.ProviderName,
                    "SmileId",
                    StringComparison.OrdinalIgnoreCase))
            {
                return lockedResponse;
            }

            var attempts = await unitOfWork.KycVerificationAttempts
                .GetByKycVerificationIdAsync(lockedKyc.Id, transactionToken);
            lockedResponse.AttemptAllowance =
                await KycAttemptAllowanceCalculator.GetAsync(
                    unitOfWork,
                    lockedKyc.Id,
                    _kycSettings,
                    DateTime.UtcNow,
                    transactionToken);
            var latest = attempts
                .Where(x => x.ProviderName == "SmileId" &&
                            x.AttemptGroupReference == lockedKyc.ProviderReference)
                .GroupBy(x => SmileIdKycProvider.CanonicalFlow(x.FlowType))
                .Select(group => group.OrderByDescending(x => x.StartedAt)
                    .ThenByDescending(x => x.CreatedAt)
                    .First())
                .ToDictionary(x => SmileIdKycProvider.CanonicalFlow(x.FlowType));
            var roles = await unitOfWork.UserRoles.GetByUserIdAsync(
                userId,
                transactionToken);
            var requiredFlows = roles.Any(x => x.Role.Name == RoleNames.Driver)
                ? new[] { SmileIdKycProvider.DriverLicenseFlow }
                : new[] { SmileIdKycProvider.IdentityFlow };
            lockedResponse.Flows = requiredFlows.Select(flow =>
            {
                latest.TryGetValue(flow, out var attempt);
                return new KycFlowStatusResponseDto
                {
                    Flow = flow,
                    Required = true,
                    Status = attempt?.Status ?? KycProviderStatus.Pending,
                    RawStatus = attempt?.RawStatus ?? "Blocked",
                    ResultCode = attempt?.ResultCode,
                    FailureReason = attempt?.FailureReason,
                    IdType = attempt?.IdentityType,
                    VerificationMethod = attempt?.VerificationMethod,
                    CallbackConfirmed = attempt?.ProviderEventTimestamp.HasValue == true,
                    CanRetry = lockedKyc.Status != KycStatus.Approved &&
                               attempt?.Status == KycProviderStatus.Rejected &&
                               lockedResponse.AttemptAllowance.CanAttempt
                };
            }).ToList();
            var latestAttempt = latest.Values
                .OrderByDescending(attempt => attempt.StartedAt)
                .ThenByDescending(attempt => attempt.CreatedAt)
                .FirstOrDefault();
            lockedResponse.SelectedIdType = latestAttempt?.IdentityType;
            lockedResponse.VerificationMethod = latestAttempt?.VerificationMethod;
            lockedResponse.CallbackConfirmed = lockedResponse.Flows.Count > 0 &&
                                                lockedResponse.Flows.All(flow =>
                                                    flow.CallbackConfirmed);
            lockedResponse.CanRetry = lockedResponse.Flows.Any(flow => flow.CanRetry);

            if (lockedResponse.CanRetry && lockedKyc.Status != KycStatus.Rejected)
            {
                var rejectedAttempt = latest.Values
                    .Where(attempt => attempt.Status == KycProviderStatus.Rejected)
                    .OrderByDescending(attempt => attempt.StartedAt)
                    .ThenByDescending(attempt => attempt.CreatedAt)
                    .First();
                lockedKyc.Status = KycStatus.Rejected;
                lockedKyc.ProviderStatus = rejectedAttempt.ResultCode ??
                                           rejectedAttempt.RawStatus ??
                                           "Rejected";
                lockedKyc.RejectionReason = rejectedAttempt.FailureReason;
                lockedKyc.VerifiedAt = null;
                lockedKyc.LastProviderUpdatedAt =
                    rejectedAttempt.ProviderUpdatedAt ?? DateTime.UtcNow;
                unitOfWork.KycVerifications.Update(lockedKyc);
                await unitOfWork.SaveChangesAsync(transactionToken);
                lockedResponse.Status = KycStatus.Rejected;
                lockedResponse.ProviderStatus = lockedKyc.ProviderStatus;
                lockedResponse.RejectionReason = lockedKyc.RejectionReason;
            }

            return lockedResponse;
        }, cancellationToken);
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
