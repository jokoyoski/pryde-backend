using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.DependencyInjection;
using Pryde.Services.Providers.Kyc;
using Pryde.Services.Providers.SmileId;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class SmileIdKycProviderTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PassengerSessionPersistsOneBiometricJobBeforeReturning()
    {
        var context = Context(RoleNames.Passenger);

        var result = await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId));

        Assert.Equal("SmileId", result.Provider);
        Assert.Equal("HostedRedirect", result.IntegrationType);
        var session = Assert.Single(result.Sessions);
        Assert.Equal(SmileIdKycProvider.BiometricFlow, session.Flow);
        Assert.StartsWith("PRYDE-SMILE-", session.JobId);
        Assert.Equal("https://links.usesmileid.com/test", session.VerificationUrl);
        Assert.True(session.Required);
        Assert.Equal("Pending", session.Status);
        var request = Assert.Single(context.ApiClient.LinkRequests);
        Assert.Equal($"pryde-{context.UserId:N}", request.UserId);
        Assert.Equal(RoleNames.Passenger, request.Role);
        Assert.Collection(
            request.IdentityOptions,
            option =>
            {
                Assert.Equal("NIN_SLIP", option.IdType);
                Assert.Equal("biometric_kyc", option.VerificationMethod);
            },
            option =>
            {
                Assert.Equal("VOTER_ID", option.IdType);
                Assert.Equal("biometric_kyc", option.VerificationMethod);
            },
            option =>
            {
                Assert.Equal("PASSPORT", option.IdType);
                Assert.Equal("doc_verification", option.VerificationMethod);
            });
        Assert.DoesNotContain("api-key", JsonSerializer.Serialize(result));
        Assert.Single(context.UnitOfWork.KycVerificationAttemptRepository.Items);
        Assert.True(context.UnitOfWork.SaveChangesCount > 0);
        Assert.Equal(KycStatus.Pending, CurrentKyc(context).Status);
    }

    [Fact]
    public async Task PassengerLinkContainsOnlyEnabledPassengerOptions()
    {
        var context = Context(RoleNames.Passenger);

        await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId));

        var options = Assert.Single(context.ApiClient.LinkRequests)
            .IdentityOptions;
        Assert.Equal(
            ["NIN_SLIP", "VOTER_ID", "PASSPORT"],
            options.Select(option => option.IdType).ToArray());
        Assert.DoesNotContain(options, option => option.IdType == "BVN");
        Assert.DoesNotContain(options, option => option.IdType == "DRIVERS_LICENSE");
    }

    [Fact]
    public async Task HostedRedirectDoesNotApproveAndMineExposesRequiredFlows()
    {
        var context = Context(RoleNames.Driver);
        var result = await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId));

        Assert.Equal("HostedRedirect", result.IntegrationType);
        Assert.Equal(KycStatus.Pending, CurrentKyc(context).Status);
        var mine = await new KycService(context.UnitOfWork)
            .GetMineAsync(context.UserId);
        Assert.Equal(KycStatus.Pending, mine.Status);
        var licence = Assert.Single(mine.Flows);
        Assert.Equal(SmileIdKycProvider.DriverLicenceFlow, licence.Flow);
        Assert.True(licence.Required);
        Assert.Equal(KycProviderStatus.Pending, licence.Status);
        Assert.False(licence.CallbackConfirmed);
    }

    [Fact]
    public async Task DriverSessionContainsOnlyDriversLicenceDocumentVerification()
    {
        var context = Context(RoleNames.Driver);

        var result = await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId));

        var licence = Assert.Single(result.Sessions);
        Assert.Equal(SmileIdKycProvider.DriverLicenceFlow, licence.Flow);
        Assert.Equal("Pending", licence.Status);
        Assert.NotNull(licence.VerificationUrl);
        var option = Assert.Single(context.ApiClient.LinkRequests.Single().IdentityOptions);
        Assert.Equal("DRIVERS_LICENSE", option.IdType);
        Assert.Equal("doc_verification", option.VerificationMethod);
        Assert.Single(context.UnitOfWork.KycVerificationAttemptRepository.Items);
    }

    [Fact]
    public async Task PassengerRequiresBothFinalBiometricResults()
    {
        var context = Context(RoleNames.Passenger);
        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId))).Sessions);

        await context.Provider.ProcessCallbackAsync(Callback(
            session, "0810", "Machine comparison passed", pascalCase: true));

        Assert.Equal(KycStatus.Submitted, CurrentKyc(context).Status);

        await context.Provider.ProcessCallbackAsync(Callback(
            session,
            "1012",
            "ID authority returned a record",
            timestamp: Now.AddSeconds(1).ToString("O")));

        Assert.Equal(KycStatus.Approved, CurrentKyc(context).Status);
        Assert.Equal(KycProviderStatus.Approved, CurrentAttempt(context, session).Status);
        Assert.Equal(
            NotificationType.KycApproved,
            Assert.Single(context.UnitOfWork.NotificationRepository.Items).Type);
    }

    [Fact]
    public async Task DriverIsNotApprovedUntilIdentityAndLicenceJobsSucceed()
    {
        var context = Context(RoleNames.Driver);
        var licence = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId))).Sessions);

        await context.Provider.ProcessCallbackAsync(Callback(licence, "0810", "Document verified"));

        Assert.Equal(KycStatus.Approved, CurrentKyc(context).Status);
    }

    [Fact]
    public async Task DocumentApprovedWithAttentionUsesDocumentedFinalMapping()
    {
        var context = Context(RoleNames.Driver);
        var licence = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId))).Sessions);

        await context.Provider.ProcessCallbackAsync(Callback(
            licence,
            "0817",
            "Approved with Attention - Document is Expired"));

        var attempt = CurrentAttempt(context, licence);
        Assert.Equal(KycProviderStatus.Approved, attempt.Status);
        Assert.Equal("0817", attempt.ResultCode);
        Assert.Contains("Expired", attempt.ResultText);
        Assert.Equal(KycStatus.Approved, CurrentKyc(context).Status);
    }

    [Fact]
    public async Task DuplicateCallbackDoesNotMutateOrSaveAgain()
    {
        var context = Context(RoleNames.Passenger);
        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId))).Sessions);
        var payload = Callback(session, "0810", "Passed");

        await context.Provider.ProcessCallbackAsync(payload);
        var saveCount = context.UnitOfWork.SaveChangesCount;
        var updatedAt = CurrentAttempt(context, session).ProviderUpdatedAt;

        await context.Provider.ProcessCallbackAsync(payload);

        Assert.Equal(saveCount, context.UnitOfWork.SaveChangesCount);
        Assert.Equal(updatedAt, CurrentAttempt(context, session).ProviderUpdatedAt);
    }

    [Fact]
    public async Task InvalidStaleAndMismatchedCallbacksAreRejected()
    {
        var context = Context(RoleNames.Passenger);
        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId))).Sessions);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            context.Provider.ProcessCallbackAsync(Callback(
                session, "0810", "Passed", signature: "invalid")));
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            context.Provider.ProcessCallbackAsync(Callback(
                session, "0810", "Passed", timestamp: Now.AddMinutes(-6).ToString("O"))));
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Provider.ProcessCallbackAsync(Callback(
                session, "0810", "Passed", userId: "pryde-wrong-user")));
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Provider.ProcessCallbackAsync(Callback(
                session, "0810", "Passed", role: RoleNames.Driver)));
        await context.Provider.ProcessCallbackAsync(Callback(
            session,
            "0810",
            "Passed",
            timestamp: Now.AddSeconds(1).ToString("O")));
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Provider.ProcessCallbackAsync(Callback(
                session,
                "1012",
                "ID returned",
                timestamp: Now.AddSeconds(2).ToString("O"),
                idType: "BVN")));

        Assert.Equal(KycStatus.Submitted, CurrentKyc(context).Status);
    }

    [Fact]
    public async Task ReusedSignedTimestampCannotAlterAnAcceptedCallback()
    {
        var context = Context(RoleNames.Passenger);
        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId))).Sessions);
        await context.Provider.ProcessCallbackAsync(Callback(session, "0810", "Passed"));

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            context.Provider.ProcessCallbackAsync(Callback(
                session,
                "0811",
                "Forged failure using captured signature")));

        Assert.Equal("0810", CurrentAttempt(context, session).ResultCode);
    }

    [Fact]
    public async Task UnknownJobCannotApproveAnyUser()
    {
        var context = Context(RoleNames.Passenger);
        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId))).Sessions);
        var unknown = new KycProviderSession
        {
            JobId = $"PRYDE-SMILE-{Guid.NewGuid():N}",
            Flow = session.Flow
        };
        CallbackUsers[unknown.JobId] = $"pryde-{context.UserId:N}";

        await Assert.ThrowsAsync<NotFoundException>(() =>
            context.Provider.ProcessCallbackAsync(Callback(unknown, "0810", "Passed")));

        Assert.Equal(KycStatus.Pending, CurrentKyc(context).Status);
    }

    [Fact]
    public async Task LegacyNinV2AttemptWithoutOptionSnapshotRemainsCallbackCompatible()
    {
        var context = Context(RoleNames.Passenger);
        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId))).Sessions);
        CurrentAttempt(context, session).IdentityOptions = null;

        await context.Provider.ProcessCallbackAsync(Callback(
            session,
            "0810",
            "Machine comparison passed",
            idType: "NIN_V2",
            legacyPartnerParams: true));

        var attempt = CurrentAttempt(context, session);
        Assert.Equal("NIN_V2", attempt.IdentityType);
        Assert.Equal("biometric_kyc", attempt.VerificationMethod);
        Assert.Equal(KycProviderStatus.Submitted, attempt.Status);
    }

    [Fact]
    public async Task RetryCreatesNewJobsAndPreservesAttemptHistory()
    {
        var context = Context(RoleNames.Passenger);
        var first = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId))).Sessions);
        await context.Provider.ProcessCallbackAsync(Callback(first, "0811", "Face mismatch"));

        var second = Assert.Single((await context.Provider.RetryAsync(
            new KycProviderRequest(context.UserId))).Sessions);

        Assert.NotEqual(first.JobId, second.JobId);
        Assert.Equal(2, context.UnitOfWork.KycVerificationAttemptRepository.Items.Count);
        Assert.Equal(KycProviderStatus.Rejected, CurrentAttempt(context, first).Status);
        Assert.Equal(KycProviderStatus.Pending, CurrentAttempt(context, second).Status);
    }

    [Fact]
    public async Task JobStatusHistoryRecoversMissedBiometricCallbacks()
    {
        var context = Context(RoleNames.Passenger);
        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId))).Sessions);
        context.ApiClient.JobStatus = new SmileIdJobStatusResponse
        {
            Code = "2302",
            History =
            [
                StatusResult(session, "0810", "Machine pass"),
                StatusResult(session, "1012", "ID authority success")
            ]
        };

        await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId));

        Assert.Equal(KycStatus.Approved, CurrentKyc(context).Status);
        Assert.Equal(KycProviderStatus.Approved, CurrentAttempt(context, session).Status);
    }

    [Theory]
    [InlineData("Sandbox")]
    [InlineData("Production")]
    public async Task EnvironmentUsesHostedLinkFromConfiguredApiClient(
        string environment)
    {
        var context = Context(RoleNames.Passenger, environment);

        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId))).Sessions);

        Assert.Equal("https://links.usesmileid.com/test", session.VerificationUrl);
        Assert.Equal(environment, Settings(environment).Environment);
    }

    [Fact]
    public void ResolverSwitchesToSmileIdUsingOnlyActiveProvider()
    {
        var smile = new StubProvider("SmileId");
        var resolver = new KycProviderResolver(
            [new StubProvider("Dojah"), smile],
            Options.Create(new KycSettings { ActiveProvider = "SmileId" }));

        Assert.Same(smile, resolver.ResolveActive());
    }

    [Fact]
    public async Task MissingDojahCredentialsDoNotAffectSmileIdStartup()
    {
        var values = new Dictionary<string, string?>
        {
            ["Kyc:ActiveProvider"] = "SmileId",
            ["SmileId:PartnerId"] = "partner-test",
            ["SmileId:ApiKey"] = "api-key",
            ["SmileId:Environment"] = "Sandbox",
            ["SmileId:SandboxBaseUrl"] = "https://testapi.smileidentity.com/",
            ["SmileId:ProductionBaseUrl"] = "https://api.smileidentity.com/",
            ["SmileId:CallbackUrl"] = "https://example.test/api/v1/kyc/providers/smile-id/callback",
            ["SmileId:RedirectUrl"] = "https://app.example.test/onboarding/kyc",
            ["SmileId:CompanyName"] = "Pryde",
            ["SmileId:DataPrivacyPolicyUrl"] = "https://example.test/privacy",
            ["SmileId:PassengerIdentityOptions:0:IdType"] = "NIN_SLIP",
            ["SmileId:PassengerIdentityOptions:0:VerificationMethod"] = "biometric_kyc",
            ["SmileId:DriverIdentityOptions:0:IdType"] = "DRIVERS_LICENSE",
            ["SmileId:DriverIdentityOptions:0:VerificationMethod"] = "doc_verification"
        };
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IUnitOfWork>(new TestUnitOfWork());
        builder.Services.AddSingleton<INotificationService>(serviceProvider =>
            new NotificationService(serviceProvider.GetRequiredService<IUnitOfWork>()));
        builder.Services.AddDojahIntegration(new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build());
        using var host = builder.Build();

        await host.StartAsync();
        using var scope = host.Services.CreateScope();
        Assert.Equal("SmileId", scope.ServiceProvider
            .GetRequiredService<IKycProviderResolver>()
            .ResolveActive().Name);
        await host.StopAsync();
    }

    [Fact]
    public void SelectedSmileIdConfigurationAndProviderNameFailClearly()
    {
        var invalidSmile = new SmileIdSettingsValidator(Options.Create(
            new KycSettings { ActiveProvider = "SmileId" }))
            .Validate(null, new SmileIdSettings());
        var invalidProvider = new KycSettingsValidator().Validate(
            null,
            new KycSettings { ActiveProvider = "Unknown" });

        Assert.True(invalidSmile.Failed);
        Assert.Contains("PartnerId", invalidSmile.FailureMessage);
        Assert.True(invalidProvider.Failed);
        Assert.Contains("Dojah or SmileId", invalidProvider.FailureMessage);
    }

    [Fact]
    public void InvalidConfiguredIdentityProductCombinationFailsClearly()
    {
        var settings = Settings();
        settings.PassengerIdentityOptions =
        [
            new()
            {
                IdType = "PASSPORT",
                VerificationMethod = "biometric_kyc"
            }
        ];

        var result = new SmileIdSettingsValidator(Options.Create(
            new KycSettings { ActiveProvider = "SmileId" }))
            .Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains(
            "PASSPORT|biometric_kyc",
            result.FailureMessage);
    }

    [Fact]
    public void DisabledBvnDoesNotSatisfyRequiredPassengerOptions()
    {
        var settings = Settings();
        settings.PassengerIdentityOptions =
        [
            new()
            {
                IdType = "BVN",
                VerificationMethod = "biometric_kyc",
                Enabled = false
            }
        ];

        var result = new SmileIdSettingsValidator(Options.Create(
            new KycSettings { ActiveProvider = "SmileId" }))
            .Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains("at least one enabled option", result.FailureMessage);
    }

    private static TestContext Context(string role, string environment = "Sandbox")
    {
        var unitOfWork = new TestUnitOfWork();
        var userId = Guid.NewGuid();
        unitOfWork.UserRoleRepository.Items.Add(new UserRole
        {
            UserId = userId,
            Role = new Role { Name = role }
        });
        var apiClient = new StubSmileIdApiClient();
        var settings = Settings(environment);
        var provider = new SmileIdKycProvider(
            unitOfWork,
            apiClient,
            Options.Create(settings),
            NullLogger<SmileIdKycProvider>.Instance,
            new NotificationService(unitOfWork),
            new FixedTimeProvider(Now));
        return new TestContext(unitOfWork, provider, apiClient, userId);
    }

    private static SmileIdSettings Settings(string environment = "Sandbox") => new()
    {
        PartnerId = "partner-test",
        ApiKey = "api-key",
        Environment = environment,
        SandboxBaseUrl = "https://testapi.smileidentity.com/",
        ProductionBaseUrl = "https://api.smileidentity.com/",
        CallbackUrl = "https://example.test/api/v1/kyc/providers/smile-id/callback",
        RedirectUrl = "https://app.example.test/onboarding/kyc",
        CompanyName = "Pryde",
        DataPrivacyPolicyUrl = "https://example.test/privacy",
        MaximumCallbackAgeMinutes = 5,
        PassengerIdentityOptions =
        [
            new() { IdType = "NIN_SLIP", VerificationMethod = "biometric_kyc" },
            new() { IdType = "VOTER_ID", VerificationMethod = "biometric_kyc" },
            new() { IdType = "BVN", VerificationMethod = "biometric_kyc", Enabled = false },
            new() { IdType = "PASSPORT", VerificationMethod = "doc_verification" }
        ],
        DriverIdentityOptions =
        [
            new() { IdType = "DRIVERS_LICENSE", VerificationMethod = "doc_verification" }
        ]
    };

    private static readonly ConcurrentDictionary<string, string> CallbackUsers = new();

    private static ReadOnlyMemory<byte> Callback(
        KycProviderSession session,
        string resultCode,
        string resultText,
        bool pascalCase = false,
        string signature = "valid",
        string? timestamp = null,
        string? userId = null,
        string? role = null,
        string? idType = null,
        bool legacyPartnerParams = false)
    {
        var partnerParams = new Dictionary<string, object?>
        {
            ["job_id"] = session.JobId,
            ["user_id"] = userId ?? CallbackUsers[session.JobId]
        };
        if (legacyPartnerParams)
        {
            partnerParams["job_type"] = session.Flow == SmileIdKycProvider.DriverLicenceFlow ? "6" : "1";
        }
        else
        {
            partnerParams["flow"] = session.Flow;
            partnerParams["role"] = role ?? (session.Flow == SmileIdKycProvider.DriverLicenceFlow
                ? RoleNames.Driver
                : RoleNames.Passenger);
        }
        var payload = new Dictionary<string, object?>
        {
            ["signature"] = signature,
            ["timestamp"] = timestamp ?? Now.ToString("O"),
            [pascalCase ? "ResultCode" : "result_code"] = resultCode,
            [pascalCase ? "ResultText" : "result_text"] = resultText,
            [pascalCase ? "SmileJobID" : "smile_job_id"] = "smile-internal",
            [pascalCase ? "Country" : "country"] = "NG",
            [pascalCase ? "IDType" : "id_type"] = idType ?? (session.Flow == SmileIdKycProvider.BiometricFlow
                ? "NIN_SLIP"
                : "DRIVERS_LICENSE"),
            [pascalCase ? "PartnerParams" : "partner_params"] = partnerParams
        };
        return JsonSerializer.SerializeToUtf8Bytes(payload);
    }

    private static KycVerification CurrentKyc(TestContext context) =>
        Assert.Single(context.UnitOfWork.KycVerificationRepository.Items);

    private static KycVerificationAttempt CurrentAttempt(
        TestContext context,
        KycProviderSession session) =>
        context.UnitOfWork.KycVerificationAttemptRepository.Items.Single(
            x => x.CorrelationReference == session.JobId);

    private static SmileIdResultPayload StatusResult(
        KycProviderSession session,
        string code,
        string text) => new()
        {
            ResultCode = code,
            ResultText = text,
            SmileJobId = "smile-internal",
            Country = "NG",
            IdType = session.Flow == SmileIdKycProvider.BiometricFlow ? "NIN_SLIP" : "DRIVERS_LICENSE",
            PartnerParams = new SmileIdPartnerParams
            {
                JobId = session.JobId,
                UserId = CallbackUsers[session.JobId],
                Flow = session.Flow,
                Role = session.Flow == SmileIdKycProvider.DriverLicenceFlow
                    ? RoleNames.Driver
                    : RoleNames.Passenger
            }
        };

    private sealed record TestContext(
        TestUnitOfWork UnitOfWork,
        SmileIdKycProvider Provider,
        StubSmileIdApiClient ApiClient,
        Guid UserId);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubSmileIdApiClient : ISmileIdApiClient
    {
        public List<SmileIdLinkRequest> LinkRequests { get; } = [];
        public SmileIdJobStatusResponse JobStatus { get; set; } = new()
        {
            Code = "2304"
        };

        public Task<SmileIdLinkResponse> CreateSingleUseLinkAsync(
            SmileIdLinkRequest request,
            CancellationToken cancellationToken = default)
        {
            LinkRequests.Add(request);
            CallbackUsers[request.JobId] = request.UserId;
            return Task.FromResult(new SmileIdLinkResponse
            {
                Link = "https://links.usesmileid.com/test",
                ReferenceId = $"link-{request.JobId}"
            });
        }

        public Task<SmileIdJobStatusResponse> GetJobStatusAsync(
            string userId,
            string jobId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(JobStatus);

        public bool ValidateSignature(string timestamp, string signature) =>
            signature == "valid";
    }

    private sealed class StubProvider(string name) : IKycProvider
    {
        public string Name { get; } = name;
        public Task<KycProviderResult> CreateSessionAsync(
            KycProviderRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<KycProviderResult> RetryAsync(
            KycProviderRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

public class SmileIdApiClientTests
{
    [Theory]
    [InlineData("https://testapi.smileidentity.com/")]
    [InlineData("https://api.smileidentity.com/")]
    public async Task JobStatusUsesDocumentedUrlBodyAndSignedResponse(string baseUrl)
    {
        const string timestamp = "2026-08-13T12:00:00.000Z";
        const string partnerId = "partner-test";
        const string apiKey = "api-key";
        var responseSignature = Signature(timestamp, partnerId, apiKey);
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                timestamp,
                signature = responseSignature,
                code = "2302",
                history = Array.Empty<object>()
            }), Encoding.UTF8, "application/json")
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        var api = new SmileIdApiClient(
            client,
            Options.Create(new SmileIdSettings
            {
                PartnerId = partnerId,
                ApiKey = apiKey
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero)));

        await api.GetJobStatusAsync("pryde-user", "job-1");

        Assert.Equal(new Uri(new Uri(baseUrl), "v1/job_status"), handler.RequestUri);
        using var json = JsonDocument.Parse(handler.Body!);
        Assert.Equal("pryde-user", json.RootElement.GetProperty("user_id").GetString());
        Assert.Equal("job-1", json.RootElement.GetProperty("job_id").GetString());
        Assert.Equal(partnerId, json.RootElement.GetProperty("partner_id").GetString());
        Assert.True(json.RootElement.GetProperty("history").GetBoolean());
        Assert.False(json.RootElement.GetProperty("image_links").GetBoolean());
        Assert.Equal(
            Signature(timestamp, partnerId, apiKey),
            json.RootElement.GetProperty("signature").GetString());
    }

    [Theory]
    [InlineData("https://testapi.smileidentity.com/")]
    [InlineData("https://api.smileidentity.com/")]
    public async Task CreateLinkUsesDocumentedSingleUserHostedContract(string baseUrl)
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"link\":\"https://links.usesmileid.com/abc\",\"ref_id\":\"ref-1\"}",
                Encoding.UTF8,
                "application/json")
        });
        var api = new SmileIdApiClient(
            new HttpClient(handler) { BaseAddress = new Uri(baseUrl) },
            Options.Create(new SmileIdSettings
            {
                PartnerId = "partner-test",
                ApiKey = "api-key",
                CompanyName = "Pryde",
                CallbackUrl = "https://api.example.test/callback",
                RedirectUrl = "https://app.example.test/onboarding/kyc",
                DataPrivacyPolicyUrl = "https://example.test/privacy"
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero)));

        var result = await api.CreateSingleUseLinkAsync(new SmileIdLinkRequest(
            "Pryde identity job-1",
            "pryde-user",
            "job-1",
            RoleNames.Passenger,
            SmileIdKycProvider.BiometricFlow,
            [
                new("NG", "NIN_SLIP", "biometric_kyc"),
                new("NG", "PASSPORT", "doc_verification")
            ]));

        Assert.Equal("https://links.usesmileid.com/abc", result.Link);
        Assert.Equal(new Uri(new Uri(baseUrl), "v1/smile_links"), handler.RequestUri);
        using var json = JsonDocument.Parse(handler.Body!);
        var root = json.RootElement;
        Assert.True(root.GetProperty("is_single_use").GetBoolean());
        Assert.Equal("pryde-user", root.GetProperty("user_id").GetString());
        Assert.Equal("https://api.example.test/callback", root.GetProperty("callback_url").GetString());
        Assert.Equal("https://app.example.test/onboarding/kyc", root.GetProperty("redirect_url").GetString());
        Assert.Equal("job-1", root.GetProperty("partner_params").GetProperty("job_id").GetString());
        Assert.Equal(RoleNames.Passenger, root.GetProperty("partner_params").GetProperty("role").GetString());
        var idTypes = root.GetProperty("id_types").EnumerateArray().ToList();
        Assert.Equal(2, idTypes.Count);
        Assert.Equal("NIN_SLIP", idTypes[0].GetProperty("id_type").GetString());
        Assert.Equal("biometric_kyc", idTypes[0].GetProperty("verification_method").GetString());
        Assert.Equal("PASSPORT", idTypes[1].GetProperty("id_type").GetString());
        Assert.Equal("doc_verification", idTypes[1].GetProperty("verification_method").GetString());
    }

    private static string Signature(string timestamp, string partnerId, string apiKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToBase64String(hmac.ComputeHash(
            Encoding.UTF8.GetBytes(timestamp + partnerId + "sid_request")));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
}
