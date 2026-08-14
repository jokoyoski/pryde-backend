using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Providers.Kyc;
using Pryde.Services.Providers.SmileId;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;

namespace Pryde.Services.Service.Implementation;

public sealed class SmileIdKycProvider(
    IUnitOfWork unitOfWork,
    ISmileIdApiClient apiClient,
    IOptions<SmileIdSettings> options,
    ILogger<SmileIdKycProvider> logger,
    INotificationService notificationService,
    TimeProvider? timeProvider = null) : IKycProvider, ISmileIdKycService
{
    public const string ProviderName = "SmileId";
    public const string BiometricFlow = "BiometricKyc";
    public const string DriverLicenceFlow = "DriverLicenceDocumentVerification";
    private const int BiometricJobType = 1;
    private const int DocumentVerificationJobType = 6;
    private const string Country = "NG";
    private const string DriverLicenceIdType = "DRIVERS_LICENSE";

    private static readonly HashSet<string> BiometricActionSuccessCodes =
        ["0810", "1210"];
    private static readonly HashSet<string> BiometricIdentitySuccessCodes =
        ["1012"];
    private static readonly HashSet<string> BiometricProvisionalCodes =
        ["0812", "0814", "0815", "1213"];
    private static readonly HashSet<string> BiometricFailureCodes =
        ["0001", "0811", "0813", "0908", "0911", "0912", "1013", "1014", "1015", "1016", "1211", "1212", "2205", "2209", "2212", "2215", "2220"];
    private static readonly HashSet<string> DocumentFailureCodes =
        ["0001", "0811", "0812", "0816", "1014", "2205", "2212", "2215", "2220"];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SmileIdSettings _settings = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public string Name => ProviderName;

    public Task<KycProviderResult> CreateSessionAsync(
        KycProviderRequest request,
        CancellationToken cancellationToken = default) =>
        CreateOrReturnSessionAsync(request.UserId, false, cancellationToken);

    public Task<KycProviderResult> RetryAsync(
        KycProviderRequest request,
        CancellationToken cancellationToken = default) =>
        CreateOrReturnSessionAsync(request.UserId, true, cancellationToken);

    public async Task ProcessCallbackAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        SmileIdCallbackPayload callback;
        try
        {
            callback = JsonSerializer.Deserialize<SmileIdCallbackPayload>(payload.Span, JsonOptions)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new ValidationException("Invalid Smile ID callback payload.");
        }

        var eventTimestamp = ValidateCallbackAuthentication(
            callback.Timestamp,
            callback.Signature);
        var payloadHash = Convert.ToHexString(SHA256.HashData(payload.Span));
        var partnerParams = callback.PartnerParams ?? callback.PartnerParamsSnakeCase
            ?? throw new ValidationException("Smile ID callback PartnerParams are required.");

        await ProcessResultAsync(
            partnerParams,
            callback.ResultCode ?? callback.ResultCodeSnakeCase,
            callback.ResultText ?? callback.ResultTextSnakeCase,
            callback.SmileJobId ?? callback.SmileJobIdSnakeCase,
            callback.Country,
            callback.IdType ?? callback.IdTypeSnakeCase,
            eventTimestamp,
            payloadHash,
            cancellationToken);
    }

    private async Task<KycProviderResult> CreateOrReturnSessionAsync(
        Guid userId,
        bool retry,
        CancellationToken cancellationToken)
    {
        var roles = await unitOfWork.UserRoles.GetByUserIdAsync(userId, cancellationToken);
        var roleNames = roles.Select(x => x.Role.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isDriver = roleNames.Contains(RoleNames.Driver);
        if (!isDriver && !roleNames.Contains(RoleNames.Passenger))
        {
            throw new ValidationException("Smile ID KYC supports Passenger and Driver roles only.");
        }

        if (!retry)
        {
            await RecoverPendingAttemptsAsync(userId, cancellationToken);
        }

        var prepared = await unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
        {
            var kyc = await unitOfWork.KycVerifications.GetByUserIdForUpdateAsync(
                userId,
                transactionToken);

            if (kyc is null)
            {
                kyc = new KycVerification { UserId = userId };
                await unitOfWork.KycVerifications.CreateAsync(kyc, transactionToken);
            }
            else if (retry && kyc.Status != KycStatus.Rejected)
            {
                throw new ConflictException("Only rejected KYC verification can be retried.");
            }
            else if (!retry && kyc.Status == KycStatus.Rejected)
            {
                throw new ConflictException("Rejected KYC verification must be retried through the retry endpoint.");
            }

            var existing = string.Equals(
                    kyc.ProviderName,
                    ProviderName,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(kyc.ProviderReference)
                ? (await GetCurrentAttemptsAsync(kyc, transactionToken)).ToList()
                : [];
            var latestByFlow = existing
                .GroupBy(x => x.FlowType)
                .ToDictionary(
                    group => group.Key!,
                    group => group.OrderByDescending(x => x.StartedAt)
                        .ThenByDescending(x => x.CreatedAt)
                        .First());
            string? flowToCreate;
            if (retry)
            {
                flowToCreate = latestByFlow.Values
                    .Where(x => x.Status == KycProviderStatus.Rejected)
                    .OrderBy(x => x.FlowType == BiometricFlow ? 0 : 1)
                    .Select(x => x.FlowType)
                    .FirstOrDefault();
                if (flowToCreate is null)
                {
                    throw new ConflictException("No failed Smile ID flow is retryable.");
                }
            }
            else if (latestByFlow.Count == 0)
            {
                flowToCreate = isDriver
                    ? DriverLicenceFlow
                    : BiometricFlow;
            }
            else
            {
                return new PreparedLinkAttempt(
                    null,
                    kyc.Id,
                    isDriver,
                    CreateResult(kyc, existing, isDriver));
            }

            var groupReference = string.IsNullOrWhiteSpace(kyc.ProviderReference) ||
                                 !string.Equals(kyc.ProviderName, ProviderName, StringComparison.OrdinalIgnoreCase)
                ? $"SMILE-GROUP-{Guid.NewGuid():N}"
                : kyc.ProviderReference;
            var smileUserId = CreateSmileUserId(userId);
            var identityOptions = GetIdentityOptions(isDriver);
            var attempt = new KycVerificationAttempt
            {
                KycVerificationId = kyc.Id,
                ProviderName = ProviderName,
                CorrelationReference = $"PRYDE-SMILE-{Guid.NewGuid():N}",
                Status = KycProviderStatus.Pending,
                RawStatus = "CreatingLink",
                FlowType = flowToCreate,
                AttemptGroupReference = groupReference,
                ExternalUserReference = smileUserId,
                StartedAt = _timeProvider.GetUtcNow().UtcDateTime,
                IdentityOptions = string.Join(',', identityOptions.Select(option =>
                    $"{option.IdType}:{option.VerificationMethod}"))
            };
            await unitOfWork.KycVerificationAttempts.CreateAsync(attempt, transactionToken);

            kyc.ProviderName = ProviderName;
            kyc.ProviderReference = groupReference;
            kyc.ProviderStatus = "Created";
            kyc.Status = KycStatus.Pending;
            kyc.VerifiedAt = null;
            kyc.RejectionReason = null;
            kyc.LastProviderUpdatedAt = null;
            unitOfWork.KycVerifications.Update(kyc);

            await unitOfWork.SaveChangesAsync(transactionToken);
            return new PreparedLinkAttempt(
                new SmileIdLinkRequest(
                    $"Pryde {flowToCreate} {attempt.CorrelationReference}",
                    smileUserId,
                    attempt.CorrelationReference,
                    isDriver ? RoleNames.Driver : RoleNames.Passenger,
                    flowToCreate,
                    identityOptions.Select(option => new SmileIdLinkIdentityOption(
                        Country,
                        option.IdType,
                        option.VerificationMethod)).ToList()),
                kyc.Id,
                isDriver,
                null);
        }, cancellationToken);

        if (prepared.ExistingResult is not null)
        {
            return prepared.ExistingResult;
        }

        unitOfWork.ClearTracking();
        SmileIdLinkResponse link;
        try
        {
            link = await apiClient.CreateSingleUseLinkAsync(
                prepared.Request!,
                cancellationToken);
        }
        catch
        {
            await MarkLinkCreationFailedAsync(
                prepared.Request!.JobId,
                CancellationToken.None);
            throw;
        }

        unitOfWork.ClearTracking();
        return await unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
        {
            var attempt = await unitOfWork.KycVerificationAttempts
                .GetByCorrelationReferenceForUpdateAsync(
                    ProviderName,
                    prepared.Request!.JobId,
                    transactionToken)
                ?? throw new NotFoundException("Smile ID verification attempt was not found.");
            var kyc = await unitOfWork.KycVerifications.GetByIdForUpdateAsync(
                prepared.KycVerificationId,
                transactionToken)
                ?? throw new NotFoundException("KYC verification was not found.");

            if (string.IsNullOrWhiteSpace(attempt.VerificationUrl))
            {
                attempt.ProviderReference = link.ReferenceId;
                attempt.VerificationUrl = link.Link;
                attempt.RawStatus = "LinkCreated";
                attempt.ProviderUpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                unitOfWork.KycVerificationAttempts.Update(attempt);
                kyc.ProviderStatus = "LinkCreated";
                unitOfWork.KycVerifications.Update(kyc);
                await unitOfWork.SaveChangesAsync(transactionToken);
            }

            var attempts = (await GetCurrentAttemptsAsync(kyc, transactionToken)).ToList();
            return CreateResult(kyc, attempts, prepared.IsDriver);
        }, cancellationToken);
    }

    private async Task MarkLinkCreationFailedAsync(
        string correlationReference,
        CancellationToken cancellationToken)
    {
        unitOfWork.ClearTracking();
        await unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
        {
            var attempt = await unitOfWork.KycVerificationAttempts
                .GetByCorrelationReferenceForUpdateAsync(
                    ProviderName,
                    correlationReference,
                    transactionToken);
            if (attempt is null ||
                attempt.Status == KycProviderStatus.Rejected ||
                !string.IsNullOrWhiteSpace(attempt.VerificationUrl))
            {
                return true;
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            attempt.Status = KycProviderStatus.Rejected;
            attempt.RawStatus = "LinkCreationFailed";
            attempt.FailureReason = "Smile ID link creation failed.";
            attempt.ProviderUpdatedAt = now;
            attempt.CompletedAt = now;
            unitOfWork.KycVerificationAttempts.Update(attempt);

            var kyc = await unitOfWork.KycVerifications.GetByIdForUpdateAsync(
                attempt.KycVerificationId,
                transactionToken);
            if (kyc is not null && kyc.Status != KycStatus.Approved)
            {
                kyc.Status = KycStatus.Rejected;
                kyc.ProviderStatus = "LinkCreationFailed";
                kyc.RejectionReason = "Smile ID link creation failed.";
                kyc.LastProviderUpdatedAt = now;
                unitOfWork.KycVerifications.Update(kyc);
            }

            await unitOfWork.SaveChangesAsync(transactionToken);
            return true;
        }, cancellationToken);
    }

    private async Task RecoverPendingAttemptsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(userId, cancellationToken);
        if (kyc is null ||
            !string.Equals(kyc.ProviderName, ProviderName, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(kyc.ProviderReference) ||
            kyc.Status is KycStatus.Approved or KycStatus.Rejected)
        {
            return;
        }

        foreach (var attempt in await GetCurrentAttemptsAsync(kyc, cancellationToken))
        {
            if (attempt.Status is KycProviderStatus.Approved or KycProviderStatus.Rejected)
            {
                continue;
            }

            var status = await apiClient.GetJobStatusAsync(
                attempt.ExternalUserReference!,
                attempt.CorrelationReference,
                cancellationToken);
            if (status.Code != "2302")
            {
                continue;
            }

            var results = (status.History ?? [])
                .Where(x => string.Equals(
                    (x.PartnerParams ?? x.PartnerParamsSnakeCase)?.JobId,
                    attempt.CorrelationReference,
                    StringComparison.Ordinal))
                .ToList();
            if (results.Count == 0 && status.Result is not null)
            {
                results.Add(status.Result);
            }

            foreach (var result in results)
            {
                var partnerParams = result.PartnerParams ?? result.PartnerParamsSnakeCase;
                var resultCode = result.ResultCode ?? result.ResultCodeSnakeCase;
                if (partnerParams is null || string.IsNullOrWhiteSpace(resultCode))
                {
                    continue;
                }

                await ProcessResultAsync(
                    partnerParams,
                    resultCode,
                    result.ResultText ?? result.ResultTextSnakeCase,
                    result.SmileJobId ?? result.SmileJobIdSnakeCase,
                    result.Country,
                    result.IdType ?? result.IdTypeSnakeCase,
                    null,
                    null,
                    cancellationToken);
            }
        }
    }

    private async Task ProcessResultAsync(
        SmileIdPartnerParams partnerParams,
        string? resultCode,
        string? resultText,
        string? smileJobId,
        string? country,
        string? idType,
        DateTimeOffset? eventTimestamp,
        string? payloadHash,
        CancellationToken cancellationToken)
    {
        var jobId = Required(partnerParams.JobId, "job_id");
        var smileUserId = Required(partnerParams.UserId, "user_id");
        var code = Required(resultCode, "ResultCode/result_code");

        await unitOfWork.ExecuteInTransactionOnceAsync(async transactionToken =>
        {
            var attempt = await unitOfWork.KycVerificationAttempts.GetByCorrelationReferenceAsync(
                ProviderName,
                jobId,
                transactionToken)
                ?? throw new NotFoundException(nameof(KycVerificationAttempt), jobId);

            if (!string.Equals(attempt.ExternalUserReference, smileUserId, StringComparison.Ordinal))
            {
                throw new ValidationException("Smile ID callback user_id does not match the job.");
            }

            var expectedRole = attempt.FlowType == DriverLicenceFlow
                ? RoleNames.Driver
                : attempt.FlowType == BiometricFlow
                    ? RoleNames.Passenger
                    : throw new ValidationException("Smile ID attempt has an unsupported flow.");
            var isLegacyAttempt = string.IsNullOrWhiteSpace(attempt.IdentityOptions);
            if (isLegacyAttempt)
            {
                var expectedJobType = attempt.FlowType == BiometricFlow
                    ? BiometricJobType
                    : DocumentVerificationJobType;
                if (!TryGetJobType(partnerParams.JobType, out var jobType) ||
                    jobType != expectedJobType)
                {
                    throw new ValidationException("Smile ID callback job_type does not match the legacy job.");
                }
            }
            else if (!string.Equals(partnerParams.Flow, attempt.FlowType, StringComparison.Ordinal) ||
                     !string.Equals(partnerParams.Role, expectedRole, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("Smile ID callback role or flow does not match the job.");
            }
            var configuredOption = GetConfiguredOption(attempt, idType);

            if (!string.Equals(country?.Trim(), Country, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException(
                    "Smile ID result country does not match the required Pryde flow.");
            }

            var kyc = await unitOfWork.KycVerifications.GetByIdForUpdateAsync(
                attempt.KycVerificationId,
                transactionToken)
                ?? throw new NotFoundException(nameof(KycVerification), attempt.KycVerificationId);

            if (eventTimestamp.HasValue && attempt.ProviderEventTimestamp.HasValue)
            {
                var storedTimestamp = new DateTimeOffset(
                    DateTime.SpecifyKind(
                        attempt.ProviderEventTimestamp.Value,
                        DateTimeKind.Utc));
                if (eventTimestamp.Value < storedTimestamp)
                {
                    logger.LogInformation(
                        "Older Smile ID callback ignored for job {JobId}.",
                        jobId);
                    return true;
                }

                if (eventTimestamp.Value == storedTimestamp)
                {
                    if (string.Equals(
                            attempt.CallbackPayloadHash,
                            payloadHash,
                            StringComparison.Ordinal))
                    {
                        logger.LogInformation(
                            "Duplicate Smile ID callback ignored for job {JobId}.",
                            jobId);
                        return true;
                    }

                    throw new UnauthorizedException(
                        "Smile ID callback timestamp was replayed with altered data.");
                }
            }

            var previousStatus = attempt.Status;
            var previousAction = attempt.SmileActionSucceeded;
            var previousIdentity = attempt.SmileIdentitySucceeded;
            var previousKycStatus = kyc.Status;

            attempt.IdentityType = configuredOption.IdType;
            attempt.VerificationMethod = configuredOption.VerificationMethod;
            ApplyResult(attempt, code, configuredOption.VerificationMethod);
            if (attempt.Status == previousStatus &&
                attempt.SmileActionSucceeded == previousAction &&
                attempt.SmileIdentitySucceeded == previousIdentity &&
                attempt.ResultCode == code &&
                attempt.ResultText == resultText &&
                (string.IsNullOrWhiteSpace(smileJobId) || attempt.ProviderReference == smileJobId))
            {
                logger.LogInformation("Duplicate Smile ID callback ignored for job {JobId}.", jobId);
                return true;
            }

            attempt.RawStatus = resultText ?? code;
            attempt.ResultCode = code;
            attempt.ResultText = resultText;
            attempt.ProviderReference = string.IsNullOrWhiteSpace(smileJobId)
                ? attempt.ProviderReference
                : smileJobId;
            attempt.ProviderUpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            attempt.ProviderEventTimestamp = eventTimestamp?.UtcDateTime ??
                                             attempt.ProviderEventTimestamp;
            attempt.CallbackPayloadHash = payloadHash ?? attempt.CallbackPayloadHash;
            attempt.FailureReason = attempt.Status == KycProviderStatus.Rejected
                ? resultText ?? code
                : null;
            attempt.CompletedAt = attempt.Status is KycProviderStatus.Approved or KycProviderStatus.Rejected
                ? _timeProvider.GetUtcNow().UtcDateTime
                : null;
            unitOfWork.KycVerificationAttempts.Update(attempt);

            await RecalculateKycAsync(kyc, transactionToken);
            await unitOfWork.SaveChangesAsync(transactionToken);

            if (previousKycStatus != kyc.Status &&
                kyc.Status is KycStatus.Approved or KycStatus.Rejected)
            {
                var isApproved = kyc.Status == KycStatus.Approved;
                await notificationService.TryCreateAsync(
                    new CreateNotificationRequest
                    {
                        UserId = kyc.UserId,
                        Type = isApproved
                            ? NotificationType.KycApproved
                            : NotificationType.KycRejected,
                        Title = isApproved
                            ? "KYC approved"
                            : "KYC rejected",
                        Message = isApproved
                            ? "Your identity verification was approved."
                            : string.IsNullOrWhiteSpace(kyc.RejectionReason)
                                ? "Your identity verification was rejected."
                                : $"Your identity verification was rejected: {kyc.RejectionReason}",
                        RelatedEntityId = kyc.Id,
                        RelatedEntityType = nameof(KycVerification),
                        DeduplicationKey = isApproved
                            ? $"kyc-approved:{kyc.Id}"
                            : $"kyc-rejected:{kyc.Id}"
                    },
                    transactionToken);
            }
            return true;
        }, cancellationToken);
    }

    private void ApplyResult(
        KycVerificationAttempt attempt,
        string code,
        string verificationMethod)
    {
        if (verificationMethod == "biometric_kyc")
        {
            attempt.SmileActionSucceeded |= BiometricActionSuccessCodes.Contains(code);
            attempt.SmileIdentitySucceeded |= BiometricIdentitySuccessCodes.Contains(code);
            if (attempt.SmileActionSucceeded && attempt.SmileIdentitySucceeded)
            {
                attempt.Status = KycProviderStatus.Approved;
            }
            else if (BiometricFailureCodes.Contains(code))
            {
                attempt.Status = KycProviderStatus.Rejected;
            }
            else
            {
                attempt.Status = KycProviderStatus.Submitted;
            }
            return;
        }

        attempt.Status = code is "0810" or "0817"
            ? KycProviderStatus.Approved
            : DocumentFailureCodes.Contains(code)
                ? KycProviderStatus.Rejected
                : KycProviderStatus.Submitted;
    }

    private async Task RecalculateKycAsync(KycVerification kyc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(kyc.ProviderReference))
        {
            return;
        }

        var attempts = (await unitOfWork.KycVerificationAttempts
                .GetByKycVerificationIdAsync(kyc.Id, cancellationToken))
            .Where(x => x.ProviderName == ProviderName &&
                        x.AttemptGroupReference == kyc.ProviderReference)
            .ToList();
        var latestAttempts = attempts
            .GroupBy(x => x.FlowType)
            .Select(group => group.OrderByDescending(x => x.StartedAt)
                .ThenByDescending(x => x.CreatedAt)
                .First())
            .ToList();
        var roles = await unitOfWork.UserRoles.GetByUserIdAsync(
            kyc.UserId,
            cancellationToken);
        var requiresLicence = roles.Any(x =>
            string.Equals(x.Role.Name, RoleNames.Driver, StringComparison.OrdinalIgnoreCase));
        var nextStatus = latestAttempts.Count > 0 &&
                         latestAttempts.All(x => x.Status == KycProviderStatus.Approved) &&
                         latestAttempts.Any(x =>
                             x.FlowType == (requiresLicence
                                 ? DriverLicenceFlow
                                 : BiometricFlow) &&
                             x.Status == KycProviderStatus.Approved)
            ? KycStatus.Approved
            : latestAttempts.Any(x => x.Status == KycProviderStatus.Rejected)
                ? KycStatus.Rejected
                : KycStatus.Submitted;

        kyc.ProviderStatus = string.Join(",", latestAttempts.Select(x => $"{x.FlowType}:{x.RawStatus}"));
        kyc.LastProviderUpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        kyc.Status = nextStatus;
        kyc.VerifiedAt = nextStatus == KycStatus.Approved
            ? _timeProvider.GetUtcNow().UtcDateTime
            : null;
        kyc.RejectionReason = nextStatus == KycStatus.Rejected
            ? latestAttempts.First(x => x.Status == KycProviderStatus.Rejected).FailureReason
            : null;
        unitOfWork.KycVerifications.Update(kyc);
    }

    private async Task<IReadOnlyList<KycVerificationAttempt>> GetCurrentAttemptsAsync(
        KycVerification kyc,
        CancellationToken cancellationToken) =>
        (await unitOfWork.KycVerificationAttempts.GetByKycVerificationIdAsync(kyc.Id, cancellationToken))
            .Where(x => x.ProviderName == ProviderName &&
                        x.AttemptGroupReference == kyc.ProviderReference)
            .ToList();

    private KycProviderResult CreateResult(
        KycVerification kyc,
        IReadOnlyList<KycVerificationAttempt> attempts,
        bool isDriver)
    {
        var latestByFlow = attempts
            .GroupBy(x => x.FlowType)
            .ToDictionary(
                group => group.Key!,
                group => group.OrderByDescending(x => x.StartedAt)
                    .ThenByDescending(x => x.CreatedAt)
                    .First());
        var sessions = new List<KycProviderSession>();
        if (!isDriver && latestByFlow.TryGetValue(BiometricFlow, out var biometric))
        {
            sessions.Add(MapSession(biometric));
        }
        if (isDriver)
        {
            if (latestByFlow.TryGetValue(DriverLicenceFlow, out var licence))
            {
                sessions.Add(MapSession(licence));
            }
        }

        return new KycProviderResult
        {
            Provider = ProviderName,
            IntegrationType = "HostedRedirect",
            Reference = kyc.ProviderReference!,
            Status = ToProviderStatus(kyc.Status),
            Sessions = sessions
        };

        static KycProviderSession MapSession(KycVerificationAttempt attempt) =>
            new()
            {
                Flow = attempt.FlowType!,
                JobId = attempt.CorrelationReference,
                VerificationUrl = attempt.VerificationUrl,
                Required = true,
                Status = attempt.Status.ToString()
            };
    }

    private DateTimeOffset ValidateCallbackAuthentication(string? timestamp, string? signature)
    {
        var suppliedTimestamp = Required(timestamp, "timestamp");
        var suppliedSignature = Required(signature, "signature");
        if (!DateTimeOffset.TryParse(
                suppliedTimestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedTimestamp))
        {
            throw new UnauthorizedException("Invalid Smile ID callback timestamp.");
        }

        var age = (_timeProvider.GetUtcNow() - parsedTimestamp).Duration();
        if (age > TimeSpan.FromMinutes(_settings.MaximumCallbackAgeMinutes))
        {
            throw new UnauthorizedException("Smile ID callback timestamp is stale.");
        }

        if (!apiClient.ValidateSignature(suppliedTimestamp, suppliedSignature))
        {
            throw new UnauthorizedException("Invalid Smile ID callback signature.");
        }

        return parsedTimestamp;
    }

    private string GetBaseUrl() =>
        (_settings.Environment == SmileIdSettings.Sandbox
            ? _settings.SandboxBaseUrl
            : _settings.ProductionBaseUrl).TrimEnd('/') + "/";

    private static string CreateSmileUserId(Guid userId) =>
        $"pryde-{userId:N}";

    private IReadOnlyList<SmileIdIdentityOption> GetIdentityOptions(bool isDriver) =>
        (isDriver
                ? _settings.DriverIdentityOptions
                : _settings.PassengerIdentityOptions)
            .Where(option => option.Enabled)
            .ToList();

    private SmileIdIdentityOption GetConfiguredOption(
        KycVerificationAttempt attempt,
        string? idType)
    {
        var requiredIdType = Required(idType, "IDType/id_type");
        if (string.IsNullOrWhiteSpace(attempt.IdentityOptions))
        {
            // Compatibility for hosted jobs issued before option snapshots were persisted.
            var legacyOption = attempt.FlowType == BiometricFlow
                ? new SmileIdIdentityOption
                {
                    IdType = "NIN_V2",
                    VerificationMethod = "biometric_kyc"
                }
                : attempt.FlowType == DriverLicenceFlow
                    ? new SmileIdIdentityOption
                    {
                        IdType = DriverLicenceIdType,
                        VerificationMethod = "doc_verification"
                    }
                    : null;
            if (legacyOption is not null && string.Equals(
                    legacyOption.IdType,
                    requiredIdType,
                    StringComparison.OrdinalIgnoreCase))
            {
                return legacyOption;
            }
        }

        var configured = (attempt.IdentityOptions ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .Select(parts => new SmileIdIdentityOption
            {
                IdType = parts[0],
                VerificationMethod = parts[1]
            })
            .SingleOrDefault(option => string.Equals(
                option.IdType,
                requiredIdType,
                StringComparison.OrdinalIgnoreCase));
        if (configured is null)
        {
            throw new ValidationException(
                "Smile ID result ID type does not match the configured Pryde flow.");
        }
        return configured;
    }

    private static string Required(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ValidationException($"Smile ID {name} is required.")
            : value.Trim();

    private static bool TryGetJobType(object? value, out int jobType) =>
        int.TryParse(value?.ToString()?.Trim('"'), out jobType);

    private sealed record PreparedLinkAttempt(
        SmileIdLinkRequest? Request,
        Guid KycVerificationId,
        bool IsDriver,
        KycProviderResult? ExistingResult);

    private static KycProviderStatus ToProviderStatus(KycStatus status) => status switch
    {
        KycStatus.Approved => KycProviderStatus.Approved,
        KycStatus.Rejected => KycProviderStatus.Rejected,
        KycStatus.Submitted => KycProviderStatus.Submitted,
        _ => KycProviderStatus.Pending
    };
}
