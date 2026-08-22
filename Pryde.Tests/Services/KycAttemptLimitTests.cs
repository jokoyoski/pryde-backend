using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pryde.Api.Middleware;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Notifications.Interface;
using Pryde.Services.Providers.Kyc;
using Pryde.Services.Providers.SmileId;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Settings;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class KycAttemptLimitTests
{
    private static readonly DateTime UtcNow =
        new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AttemptsOneThroughThreeAreAllowedWithCorrectRemainingCounts()
    {
        var context = DojahContext(3);

        var first = await context.Service.CreateSessionAsync(
            context.UserId,
            "NIN_V2");
        Reject(context.UnitOfWork);
        var second = await context.Service.RetryAsync(context.UserId);
        Reject(context.UnitOfWork);
        var third = await context.Service.RetryAsync(context.UserId);

        Assert.Equal((1, 2, true), Allowance(first));
        Assert.Equal((2, 1, true), Allowance(second));
        Assert.Equal((3, 0, false), Allowance(third));
        Assert.Equal(3, context.UnitOfWork.KycVerificationAttemptRepository.Items.Count);
    }

    [Fact]
    public async Task FourthAttemptIsBlockedWithoutProviderCallOrNewRow()
    {
        var context = SmileContext(3);
        var kyc = SeedRejectedSmileKyc(context, 3);
        var originalReference = kyc.ProviderReference;

        var exception = await Assert.ThrowsAsync<KycAttemptLimitExceededException>(
            () => context.Service.RetryAsync(context.UserId));

        Assert.Equal(3, exception.AttemptAllowance.Used);
        Assert.False(exception.AttemptAllowance.CanAttempt);
        Assert.Empty(context.ApiClient.LinkRequests);
        Assert.Equal(3, context.UnitOfWork.KycVerificationAttemptRepository.Items.Count);
        Assert.Equal(KycStatus.Rejected, kyc.Status);
        Assert.Equal(originalReference, kyc.ProviderReference);
    }

    [Fact]
    public async Task LimitOneBlocksFirstRetry()
    {
        var context = SmileContext(1);
        var first = await context.Service.CreateSessionAsync(
            context.UserId,
            "NIN_V2");
        var kyc = Assert.Single(context.UnitOfWork.KycVerificationRepository.Items);
        var attempt = Assert.Single(context.UnitOfWork.KycVerificationAttemptRepository.Items);
        kyc.Status = KycStatus.Rejected;
        attempt.Status = KycProviderStatus.Rejected;
        attempt.RawStatus = "Rejected";

        var exception = await Assert.ThrowsAsync<KycAttemptLimitExceededException>(
            () => context.Service.RetryAsync(context.UserId));

        Assert.Equal((1, 0, false), Allowance(first));
        Assert.Equal(1, exception.AttemptAllowance.Used);
        Assert.Single(context.ApiClient.LinkRequests);
        Assert.Single(context.UnitOfWork.KycVerificationAttemptRepository.Items);
    }

    [Fact]
    public async Task PreviousMonthAttemptsDoNotCount()
    {
        var context = SmileContext(3);
        SeedRejectedSmileKyc(
            context,
            3,
            new DateTime(2026, 7, 31, 23, 59, 0, DateTimeKind.Utc));

        var result = await context.Service.RetryAsync(context.UserId);

        Assert.Equal((1, 2, true), Allowance(result));
        Assert.Single(context.ApiClient.LinkRequests);
        Assert.Equal(4, context.UnitOfWork.KycVerificationAttemptRepository.Items.Count);
    }

    [Fact]
    public async Task RowsSharingAttemptGroupCountOnce()
    {
        var unitOfWork = new TestUnitOfWork();
        var kyc = AddKyc(unitOfWork, KycStatus.Rejected, "group-two");
        AddAttempt(unitOfWork, kyc, "flow-one", "group-one");
        AddAttempt(unitOfWork, kyc, "flow-two", "group-one");
        AddAttempt(unitOfWork, kyc, "flow-three", "group-two");

        var allowance = await KycAttemptAllowanceCalculator.GetAsync(
            unitOfWork,
            kyc.Id,
            new KycSettings { MaxAttemptsPerMonth = 3 },
            UtcNow);

        Assert.Equal(2, allowance.Used);
        Assert.Equal(1, allowance.Remaining);
    }

    [Fact]
    public async Task LinkCreationFailedAndSoftDeletedRowsDoNotCount()
    {
        var unitOfWork = new TestUnitOfWork();
        var kyc = AddKyc(unitOfWork, KycStatus.Rejected, "active");
        AddAttempt(unitOfWork, kyc, "active", null);
        AddAttempt(unitOfWork, kyc, "failed", null).RawStatus =
            "LinkCreationFailed";
        AddAttempt(unitOfWork, kyc, "deleted", null).IsDeleted = true;

        var allowance = await KycAttemptAllowanceCalculator.GetAsync(
            unitOfWork,
            kyc.Id,
            new KycSettings { MaxAttemptsPerMonth = 3 },
            UtcNow);

        Assert.Equal(1, allowance.Used);
        Assert.Equal(2, allowance.Remaining);
    }

    [Fact]
    public async Task ReopeningPendingSessionDoesNotConsumeAttempt()
    {
        var context = SmileContext(3);

        var first = await context.Service.CreateSessionAsync(
            context.UserId,
            "NIN_V2");
        var second = await context.Service.CreateSessionAsync(context.UserId);

        Assert.Equal(first.Reference, second.Reference);
        Assert.Equal((1, 2, true), Allowance(second));
        Assert.Single(context.ApiClient.LinkRequests);
        Assert.Single(context.UnitOfWork.KycVerificationAttemptRepository.Items);
    }

    [Fact]
    public async Task GetMineDisablesKycAndFlowRetryWhenExhausted()
    {
        var context = SmileContext(3);
        SeedRejectedSmileKyc(context, 3);
        var service = new KycService(
            context.UnitOfWork,
            new NotificationService(context.UnitOfWork),
            context.KycOptions);

        var response = await service.GetMineAsync(context.UserId);

        Assert.False(response.CanRetry);
        Assert.False(response.AttemptAllowance.CanAttempt);
        Assert.Equal(3, response.AttemptAllowance.Used);
        Assert.All(response.Flows, flow => Assert.False(flow.CanRetry));
    }

    [Fact]
    public async Task ConcurrentRetriesCannotExceedLimit()
    {
        var context = DojahContext(3);
        await context.Service.CreateSessionAsync(context.UserId);
        Reject(context.UnitOfWork);
        await context.Service.RetryAsync(context.UserId);
        Reject(context.UnitOfWork);

        var results = await Task.WhenAll(
            Capture(() => context.Service.RetryAsync(context.UserId)),
            Capture(() => context.Service.RetryAsync(context.UserId)));

        Assert.Single(results, result => result is null);
        Assert.Single(results, result => result is not null);
        Assert.Equal(3, context.UnitOfWork.KycVerificationAttemptRepository.Items.Count);
    }

    [Fact]
    public async Task LegacyDojahRetryCannotBypassLimit()
    {
        var context = DojahContext(3);
        var kyc = AddKyc(
            context.UnitOfWork,
            KycStatus.Rejected,
            "attempt-three",
            context.UserId,
            "Dojah");
        AddAttempt(context.UnitOfWork, kyc, "attempt-one", null, "Dojah");
        AddAttempt(context.UnitOfWork, kyc, "attempt-two", null, "Dojah");
        AddAttempt(context.UnitOfWork, kyc, "attempt-three", null, "Dojah");

        await Assert.ThrowsAsync<KycAttemptLimitExceededException>(
            () => context.Provider.RetryAsync(context.UserId));

        Assert.Equal(3, context.UnitOfWork.KycVerificationAttemptRepository.Items.Count);
        Assert.Equal(KycStatus.Rejected, kyc.Status);
        Assert.Equal("attempt-three", kyc.ProviderReference);
    }

    [Fact]
    public async Task CallbackRecoveryIsNotBlockedAndRecoveredAttemptCountsLater()
    {
        var context = DojahContext(3);
        var kyc = AddKyc(
            context.UnitOfWork,
            KycStatus.Pending,
            "historical-active",
            context.UserId,
            "Dojah");
        kyc.CreatedAt = UtcNow.AddDays(-1);
        AddAttempt(context.UnitOfWork, kyc, "attempt-one", null, "Dojah");
        AddAttempt(context.UnitOfWork, kyc, "attempt-two", null, "Dojah");
        AddAttempt(context.UnitOfWork, kyc, "attempt-three", null, "Dojah");
        var payload = Encoding.UTF8.GetBytes(
            "{\"reference_id\":\"historical-active\",\"verification_status\":\"Failed\"}");

        await context.Provider.ProcessWebhookAsync(
            payload,
            SignDojah(payload),
            null);

        var recovered = Assert.Single(
            context.UnitOfWork.KycVerificationAttemptRepository.Items,
            attempt => attempt.CorrelationReference == "historical-active");
        Assert.Equal(kyc.CreatedAt, recovered.StartedAt);
        var allowance = await KycAttemptAllowanceCalculator.GetAsync(
            context.UnitOfWork,
            kyc.Id,
            new KycSettings { MaxAttemptsPerMonth = 3 },
            UtcNow);
        Assert.Equal(4, allowance.Used);
        await Assert.ThrowsAsync<KycAttemptLimitExceededException>(
            () => context.Provider.RetryAsync(context.UserId));
    }

    [Fact]
    public async Task LimitExceptionIsTheOnlyKycErrorMappedTo429()
    {
        var allowance = KycAttemptAllowanceCalculator.CreateEmpty(
            new KycSettings { MaxAttemptsPerMonth = 3 },
            UtcNow);
        allowance.Used = 3;
        allowance.Remaining = 0;
        allowance.CanAttempt = false;
        allowance.Description =
            "You have no KYC attempts remaining this month. You can try again next month.";
        var middleware = new ExceptionMiddleware(
            _ => throw new KycAttemptLimitExceededException(allowance),
            NullLogger<ExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var payload = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal(429, payload.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal(
            "You have reached your monthly KYC attempt limit.",
            payload.RootElement.GetProperty("message").GetString());
        var responseAllowance = payload.RootElement.GetProperty(
            "attemptAllowance");
        Assert.Equal(3, responseAllowance.GetProperty("limit").GetInt32());
        Assert.Equal(3, responseAllowance.GetProperty("used").GetInt32());
        Assert.Equal(0, responseAllowance.GetProperty("remaining").GetInt32());
        Assert.False(responseAllowance.GetProperty("canAttempt").GetBoolean());
        Assert.Equal(
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            responseAllowance.GetProperty("resetsAt").GetDateTime());
        Assert.Equal(
            "You have no KYC attempts remaining this month. You can try again next month.",
            responseAllowance.GetProperty("description").GetString());
    }

    [Fact]
    public void ConfiguredLimitMustBeAtLeastOne()
    {
        var result = new KycSettingsValidator().Validate(
            null,
            new KycSettings { MaxAttemptsPerMonth = 0 });

        Assert.True(result.Failed);
        Assert.Contains("at least 1", result.FailureMessage);
    }

    private static async Task<Exception?> Capture(
        Func<Task<KycProviderResult>> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static (int Used, int Remaining, bool CanAttempt) Allowance(
        KycProviderResult result) =>
        (result.AttemptAllowance.Used,
         result.AttemptAllowance.Remaining,
         result.AttemptAllowance.CanAttempt);

    private static void Reject(TestUnitOfWork unitOfWork)
    {
        var kyc = Assert.Single(unitOfWork.KycVerificationRepository.Items);
        kyc.Status = KycStatus.Rejected;
        kyc.ProviderStatus = "Failed";
        kyc.RejectionReason = "Rejected for test.";
        var attempt = unitOfWork.KycVerificationAttemptRepository.Items
            .OrderByDescending(x => x.StartedAt)
            .ThenByDescending(x => x.CreatedAt)
            .First();
        attempt.Status = KycProviderStatus.Rejected;
        attempt.RawStatus = "Failed";
    }

    private static KycVerification SeedRejectedSmileKyc(
        SmileTestContext context,
        int attempts,
        DateTime? startedAt = null)
    {
        var kyc = AddKyc(
            context.UnitOfWork,
            KycStatus.Rejected,
            $"group-{attempts}",
            context.UserId,
            SmileIdKycProvider.ProviderName);
        for (var index = 1; index <= attempts; index++)
        {
            var attempt = AddAttempt(
                context.UnitOfWork,
                kyc,
                $"attempt-{index}",
                $"group-{index}",
                SmileIdKycProvider.ProviderName,
                startedAt ?? UtcNow.AddMinutes(-attempts + index));
            attempt.FlowType = SmileIdKycProvider.IdentityFlow;
            attempt.Status = KycProviderStatus.Rejected;
            attempt.RawStatus = "Rejected";
            attempt.IdentityType = "NIN_V2";
            attempt.VerificationMethod = "biometric_kyc";
            attempt.IdentityOptions = "NIN_V2:biometric_kyc";
        }

        return kyc;
    }

    private static KycVerification AddKyc(
        TestUnitOfWork unitOfWork,
        KycStatus status,
        string reference,
        Guid? userId = null,
        string provider = "SmileId")
    {
        var kyc = new KycVerification
        {
            UserId = userId ?? Guid.NewGuid(),
            Status = status,
            ProviderName = provider,
            ProviderReference = reference,
            RejectionReason = status == KycStatus.Rejected
                ? "Rejected for test."
                : null
        };
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        return kyc;
    }

    private static KycVerificationAttempt AddAttempt(
        TestUnitOfWork unitOfWork,
        KycVerification kyc,
        string correlationReference,
        string? groupReference,
        string provider = "SmileId",
        DateTime? startedAt = null)
    {
        var attempt = new KycVerificationAttempt
        {
            KycVerificationId = kyc.Id,
            ProviderName = provider,
            CorrelationReference = correlationReference,
            AttemptGroupReference = groupReference,
            StartedAt = startedAt ?? UtcNow,
            Status = KycProviderStatus.Rejected,
            RawStatus = "Rejected"
        };
        unitOfWork.KycVerificationAttemptRepository.Items.Add(attempt);
        return attempt;
    }

    private static DojahTestContext DojahContext(int limit)
    {
        var unitOfWork = new TestUnitOfWork();
        var options = Options.Create(new KycSettings
        {
            ActiveProvider = "Dojah",
            MaxAttemptsPerMonth = limit
        });
        var provider = new DojahKycProvider(
            unitOfWork,
            Options.Create(DojahSettings()),
            NullLogger<DojahKycProvider>.Instance,
            new NotificationService(unitOfWork),
            options);
        var service = new KycProviderService(
            new KycProviderResolver([provider], options),
            unitOfWork,
            NullLogger<KycProviderService>.Instance,
            options);
        return new DojahTestContext(
            unitOfWork,
            provider,
            service,
            Guid.NewGuid());
    }

    private static SmileTestContext SmileContext(int limit)
    {
        var unitOfWork = new TestUnitOfWork();
        var userId = Guid.NewGuid();
        unitOfWork.UserRoleRepository.Items.Add(new UserRole
        {
            UserId = userId,
            Role = new Role { Name = "Passenger" }
        });
        var apiClient = new RecordingSmileIdApiClient();
        var kycOptions = Options.Create(new KycSettings
        {
            ActiveProvider = SmileIdKycProvider.ProviderName,
            MaxAttemptsPerMonth = limit
        });
        var provider = new SmileIdKycProvider(
            unitOfWork,
            apiClient,
            Options.Create(SmileIdSettings()),
            NullLogger<SmileIdKycProvider>.Instance,
            new NotificationService(unitOfWork),
            new NoopEmailService(),
            new FixedTimeProvider(UtcNow),
            kycOptions);
        var service = new KycProviderService(
            new KycProviderResolver([provider], kycOptions),
            unitOfWork,
            NullLogger<KycProviderService>.Instance,
            kycOptions);
        return new SmileTestContext(
            unitOfWork,
            apiClient,
            service,
            kycOptions,
            userId);
    }

    private static DojahSettings DojahSettings() => new()
    {
        Enabled = true,
        BaseUrl = "https://api.dojah.io",
        AppId = "app-test",
        PublicKey = "public-test",
        PrivateKey = "private-test",
        ShareableLink = "https://identity.dojah.io/?widget_id=widget-test"
    };

    private static string SignDojah(byte[] payload)
    {
        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(DojahSettings().PrivateKey));
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }

    private static SmileIdSettings SmileIdSettings() => new()
    {
        PartnerId = "partner-test",
        ApiKey = "api-key",
        CallbackUrl = "https://example.test/api/v1/kyc/providers/smile-id/callback",
        RedirectUrl = "https://example.test/kyc",
        CompanyName = "Pryde",
        DataPrivacyPolicyUrl = "https://example.test/privacy",
        PassengerIdentityOptions =
        [
            new SmileIdIdentityOption
            {
                IdType = "NIN_V2",
                VerificationMethod = "biometric_kyc"
            }
        ]
    };

    private sealed record DojahTestContext(
        TestUnitOfWork UnitOfWork,
        DojahKycProvider Provider,
        KycProviderService Service,
        Guid UserId);

    private sealed record SmileTestContext(
        TestUnitOfWork UnitOfWork,
        RecordingSmileIdApiClient ApiClient,
        KycProviderService Service,
        IOptions<KycSettings> KycOptions,
        Guid UserId);

    private sealed class RecordingSmileIdApiClient : ISmileIdApiClient
    {
        public List<SmileIdLinkRequest> LinkRequests { get; } = [];

        public Task<SmileIdLinkResponse> CreateSingleUseLinkAsync(
            SmileIdLinkRequest request,
            CancellationToken cancellationToken = default)
        {
            LinkRequests.Add(request);
            return Task.FromResult(new SmileIdLinkResponse
            {
                Link = $"https://links.usesmileid.com/{request.JobId}",
                ReferenceId = $"link-{request.JobId}"
            });
        }

        public Task<SmileIdJobStatusResponse> GetJobStatusAsync(
            string userId,
            string jobId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SmileIdJobStatusResponse { Code = "2304" });

        public bool ValidateSignature(string timestamp, string signature) => true;
    }

    private sealed class NoopEmailService : IEmailService
    {
        public Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(utcNow, TimeSpan.Zero);
    }
}
