using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.DependencyInjection;
using Pryde.Services.Providers.Kyc;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class KycProviderAbstractionTests
{
    [Fact]
    public void DojahIsTheDefaultProvider()
    {
        var dojah = new RecordingProvider("Dojah");
        var resolver = new KycProviderResolver(
            [dojah, new RecordingProvider("FutureProvider")],
            Options.Create(new KycSettings()));

        Assert.Same(dojah, resolver.ResolveActive());
    }

    [Fact]
    public async Task GenericSessionAndRetryUseConfiguredDojahProvider()
    {
        var dojah = new RecordingProvider("Dojah");
        var resolver = new KycProviderResolver(
            [dojah, new RecordingProvider("FutureProvider")],
            Options.Create(new KycSettings
            {
                ActiveProvider = "Dojah"
            }));
        var service = new KycProviderService(
            resolver,
            new TestUnitOfWork(),
            NullLogger<KycProviderService>.Instance);
        var userId = Guid.NewGuid();

        var session = await service.CreateSessionAsync(userId);
        var retry = await service.RetryAsync(userId);

        Assert.Equal("Dojah", session.Provider);
        Assert.Equal("Dojah", retry.Provider);
        Assert.Equal("Widget", session.IntegrationType);
        Assert.Equal("Widget", retry.IntegrationType);
        Assert.Equal([userId, userId], dojah.UserIds);
    }

    [Theory]
    [InlineData("Dojah", "SmileId")]
    [InlineData("SmileId", "Dojah")]
    public void EnvironmentActiveProviderOverridesAppsettingsValue(
        string environmentProvider,
        string appsettingsProvider)
    {
        const string key = "Kyc__ActiveProvider";
        var previous = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, environmentProvider);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Kyc:ActiveProvider"] = appsettingsProvider
                })
                .AddEnvironmentVariables()
                .Build();

            Assert.Equal(
                environmentProvider,
                configuration["Kyc:ActiveProvider"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
        }
    }

    [Fact]
    public async Task NewUserUsesActiveProvider()
    {
        var dojah = new RecordingProvider("Dojah");
        var smile = new RecordingProvider("SmileId");
        var service = CreateService(new TestUnitOfWork(), "SmileId", dojah, smile);

        var result = await service.CreateSessionAsync(Guid.NewGuid());

        Assert.Equal("SmileId", result.Provider);
        Assert.Equal("HostedRedirect", result.IntegrationType);
        Assert.StartsWith("SMILE-GROUP-", result.Reference);
        Assert.Empty(dojah.UserIds);
        Assert.Single(smile.UserIds);
    }

    [Fact]
    public async Task PendingAttemptResumesOnlyItsOriginalProvider()
    {
        var unitOfWork = new TestUnitOfWork();
        var kyc = PendingKyc("SmileId", "SMILE-GROUP-existing");
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        unitOfWork.KycVerificationAttemptRepository.Items.Add(new KycVerificationAttempt
        {
            KycVerificationId = kyc.Id,
            ProviderName = "SmileId",
            CorrelationReference = "PRYDE-SMILE-existing",
            AttemptGroupReference = kyc.ProviderReference,
            VerificationUrl = "https://links.usesmileid.com/existing"
        });
        var dojah = new RecordingProvider("Dojah");
        var smile = new RecordingProvider("SmileId");
        var service = CreateService(unitOfWork, "Dojah", dojah, smile);

        var result = await service.CreateSessionAsync(kyc.UserId);

        Assert.Equal("SmileId", result.Provider);
        Assert.Empty(dojah.UserIds);
        Assert.Single(smile.UserIds);
    }

    [Fact]
    public async Task ReferenceLessPendingSmileAttemptRemainsOwnedBySmileAfterDojahSwitch()
    {
        var unitOfWork = new TestUnitOfWork();
        var kyc = new KycVerification
        {
            UserId = Guid.NewGuid(),
            ProviderName = "SmileId",
            ProviderReference = null,
            Status = KycStatus.Pending
        };
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        unitOfWork.KycVerificationAttemptRepository.Items.Add(
            new KycVerificationAttempt
            {
                KycVerificationId = kyc.Id,
                ProviderName = "SmileId",
                CorrelationReference = "PRYDE-SMILE-reference-less",
                Status = KycProviderStatus.Pending
            });
        var dojah = new RecordingProvider("Dojah");
        var smile = new RecordingProvider("SmileId");
        var service = CreateService(unitOfWork, "Dojah", dojah, smile);

        var result = await service.CreateSessionAsync(kyc.UserId);

        Assert.Equal("SmileId", result.Provider);
        Assert.Empty(dojah.UserIds);
        Assert.Single(smile.UserIds);
        Assert.Equal("SmileId", kyc.ProviderName);
    }

    [Fact]
    public async Task LegacyDojahRouteCannotClaimReferenceLessPendingSmileAttempt()
    {
        var unitOfWork = new TestUnitOfWork();
        var kyc = new KycVerification
        {
            UserId = Guid.NewGuid(),
            ProviderName = "SmileId",
            ProviderReference = null,
            Status = KycStatus.Pending
        };
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        var provider = new DojahKycProvider(
            unitOfWork,
            Options.Create(DojahSettings()),
            NullLogger<DojahKycProvider>.Instance);

        await Assert.ThrowsAsync<ConflictException>(() =>
            provider.CreateSessionAsync(new KycProviderRequest(kyc.UserId)));

        Assert.Equal("SmileId", kyc.ProviderName);
        Assert.Null(kyc.ProviderReference);
        Assert.Empty(
            unitOfWork.KycVerificationAttemptRepository.Items);
    }

    [Fact]
    public async Task PendingDojahAttemptDoesNotSwitchToActiveSmileProvider()
    {
        var unitOfWork = new TestUnitOfWork();
        var kyc = PendingKyc("Dojah", "PRYDE-existing");
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        unitOfWork.KycVerificationAttemptRepository.Items.Add(
            new KycVerificationAttempt
            {
                KycVerificationId = kyc.Id,
                ProviderName = "Dojah",
                CorrelationReference = kyc.ProviderReference!
            });
        var dojah = new RecordingProvider("Dojah");
        var smile = new RecordingProvider("SmileId");
        var service = CreateService(unitOfWork, "SmileId", dojah, smile);

        var result = await service.CreateSessionAsync(kyc.UserId);

        Assert.Equal("Dojah", result.Provider);
        Assert.Equal("Widget", result.IntegrationType);
        Assert.Single(dojah.UserIds);
        Assert.Empty(smile.UserIds);
    }

    [Fact]
    public async Task LegacyIncompleteSmileAttemptBecomesRetryable()
    {
        var unitOfWork = new TestUnitOfWork();
        var kyc = PendingKyc("Dojah", "SMILE-GROUP-legacy");
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        var legacyAttempt = new KycVerificationAttempt
        {
            KycVerificationId = kyc.Id,
            ProviderName = "SmileId",
            CorrelationReference = "PRYDE-SMILE-legacy",
            AttemptGroupReference = kyc.ProviderReference,
            RawStatus = "CreatingLink"
        };
        unitOfWork.KycVerificationAttemptRepository.Items.Add(legacyAttempt);
        var smile = new RecordingProvider("SmileId");
        var service = CreateService(
            unitOfWork,
            "SmileId",
            new RecordingProvider("Dojah"),
            smile);

        var safeStatus = await service.CreateSessionAsync(kyc.UserId);

        Assert.Equal(KycProviderStatus.Rejected, safeStatus.Status);
        Assert.Null(safeStatus.IntegrationType);
        Assert.Empty(safeStatus.Sessions);
        Assert.Empty(smile.UserIds);

        var result = await service.RetryAsync(kyc.UserId);

        Assert.Equal("SmileId", result.Provider);
        Assert.Equal(KycProviderStatus.Rejected, legacyAttempt.Status);
        Assert.Equal(KycStatus.Rejected, kyc.Status);
        Assert.Single(smile.UserIds);
    }

    [Fact]
    public async Task SwitchingProviderOnRetryCreatesProviderSpecificReference()
    {
        var unitOfWork = new TestUnitOfWork();
        var kyc = PendingKyc("SmileId", "SMILE-GROUP-old");
        kyc.Status = KycStatus.Rejected;
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        var service = CreateService(
            unitOfWork,
            "Dojah",
            new RecordingProvider("Dojah"),
            new RecordingProvider("SmileId"));

        var result = await service.RetryAsync(kyc.UserId);

        Assert.Equal("Dojah", result.Provider);
        Assert.Equal("Widget", result.IntegrationType);
        Assert.StartsWith("PRYDE-", result.Reference);
        Assert.NotEqual(kyc.ProviderReference, result.Reference);
    }

    [Fact]
    public async Task TerminalDojahRetryUsesActiveSmileProviderAndNewSmileReference()
    {
        var unitOfWork = new TestUnitOfWork();
        var kyc = PendingKyc("Dojah", "PRYDE-old");
        kyc.Status = KycStatus.Rejected;
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        var service = CreateService(
            unitOfWork,
            "SmileId",
            new RecordingProvider("Dojah"),
            new RecordingProvider("SmileId"));

        var result = await service.RetryAsync(kyc.UserId);

        Assert.Equal("SmileId", result.Provider);
        Assert.Equal("HostedRedirect", result.IntegrationType);
        Assert.StartsWith("SMILE-GROUP-", result.Reference);
        Assert.NotEqual(kyc.ProviderReference, result.Reference);
    }

    [Fact]
    public async Task ApprovedUserReceivesNoNewProviderSession()
    {
        var unitOfWork = new TestUnitOfWork();
        var kyc = PendingKyc("SmileId", "SMILE-GROUP-approved");
        kyc.Status = KycStatus.Approved;
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        var dojah = new RecordingProvider("Dojah");
        var smile = new RecordingProvider("SmileId");
        var service = CreateService(unitOfWork, "Dojah", dojah, smile);

        var result = await service.CreateSessionAsync(kyc.UserId);

        Assert.Equal(KycProviderStatus.Approved, result.Status);
        Assert.Null(result.SessionUrl);
        Assert.Empty(result.Sessions);
        Assert.Empty(dojah.UserIds);
        Assert.Empty(smile.UserIds);
    }

    [Fact]
    public async Task ExactMixedDojahSmileResponseIsRejectedByInvariant()
    {
        var invalidDojah = new RecordingProvider(
            "Dojah",
            () => new KycProviderResult
            {
                Provider = "Dojah",
                IntegrationType = "Widget",
                Reference = "SMILE-GROUP-invalid",
                SessionUrl = "https://identity.dojah.io/?reference_id=SMILE-GROUP-invalid",
                Status = KycProviderStatus.Pending,
                ClientConfiguration = ValidDojahClientConfiguration()
            });
        var service = CreateService(
            new TestUnitOfWork(),
            "Dojah",
            invalidDojah,
            new RecordingProvider("SmileId"));

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            service.CreateSessionAsync(Guid.NewGuid()));
    }

    [Fact]
    public void DojahResponseContainingSmileSessionIsRejectedByInvariant()
    {
        var result = new KycProviderResult
        {
            Provider = "Dojah",
            IntegrationType = "Widget",
            Reference = "PRYDE-valid",
            SessionUrl = "https://identity.dojah.io/session",
            Status = KycProviderStatus.Pending,
            ClientConfiguration = ValidDojahClientConfiguration(),
            Sessions =
            [
                new KycProviderSession
                {
                    Flow = "IdentityVerification",
                    JobId = "PRYDE-SMILE-invalid",
                    VerificationUrl = "https://links.usesmileid.com/invalid",
                    Required = true,
                    Status = "Pending"
                }
            ]
        };

        Assert.Throws<ServiceUnavailableException>(() =>
            KycProviderResultInvariant.Ensure("Dojah", result));
    }

    [Fact]
    public async Task DojahProviderDoesNotReuseSmileReference()
    {
        var unitOfWork = new TestUnitOfWork();
        var kyc = PendingKyc("Dojah", "SMILE-GROUP-invalid");
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        var provider = new DojahKycProvider(
            unitOfWork,
            Options.Create(DojahSettings()),
            NullLogger<DojahKycProvider>.Instance);

        await Assert.ThrowsAsync<ConflictException>(() =>
            provider.CreateSessionAsync(new KycProviderRequest(kyc.UserId)));
    }

    [Fact]
    public async Task StartupLogsOnlyResolvedProviderName()
    {
        var services = new ServiceCollection();
        services.AddScoped<IKycProviderResolver>(_ => new KycProviderResolver(
            [new RecordingProvider("Dojah")],
            Options.Create(new KycSettings())));
        using var serviceProvider = services.BuildServiceProvider();
        var logger = new CapturingLogger<KycProviderStartupLogger>();
        var startupLogger = new KycProviderStartupLogger(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            logger);

        await startupLogger.StartAsync(CancellationToken.None);

        var message = Assert.Single(logger.Messages);
        Assert.Contains("Dojah", message);
        Assert.DoesNotContain("key", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActiveProviderIsTheOnlySwitchNeededToUseDojah()
    {
        var unitOfWork = new TestUnitOfWork();
        var settings = DojahSettings();
        settings.Enabled = false;
        var provider = new DojahKycProvider(
            unitOfWork,
            Options.Create(settings),
            NullLogger<DojahKycProvider>.Instance,
            new NotificationService(unitOfWork),
            Options.Create(new KycSettings { ActiveProvider = "Dojah" }));

        var result = await provider.CreateSessionAsync(
            new KycProviderRequest(Guid.NewGuid()));

        Assert.Equal("Dojah", result.Provider);
        Assert.Equal("Widget", result.IntegrationType);
        Assert.StartsWith("PRYDE-", result.Reference);
        Assert.DoesNotContain(
            "SMILE-GROUP-",
            result.SessionUrl,
            StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.SessionUrl);
        Assert.Equal("app-test", result.ClientConfiguration["appId"]);
        Assert.Equal("public-test", result.ClientConfiguration["publicKey"]);
        Assert.Equal("widget-test", result.ClientConfiguration["widgetId"]);
    }

    [Fact]
    public async Task GenericDojahSessionReturnsWidgetContractAndResumesSameAttempt()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = CreateActualDojahService(unitOfWork);
        var userId = Guid.NewGuid();

        var first = await service.CreateSessionAsync(userId);
        var second = await service.CreateSessionAsync(userId);

        Assert.Equal("Dojah", first.Provider);
        Assert.Equal("Widget", first.IntegrationType);
        Assert.StartsWith("PRYDE-", first.Reference);
        Assert.Equal(first.Reference, second.Reference);
        Assert.Equal(first.SessionUrl, second.SessionUrl);
        Assert.NotNull(first.SessionUrl);
        Assert.Contains(first.Reference, first.SessionUrl);
        Assert.DoesNotContain(
            "SMILE-GROUP-",
            first.SessionUrl,
            StringComparison.OrdinalIgnoreCase);
        Assert.Empty(first.Sessions);
        Assert.Equal(
            ["appId", "publicKey", "widgetId"],
            first.ClientConfiguration.Keys.Order().ToArray());
        Assert.Equal("app-test", first.ClientConfiguration["appId"]);
        Assert.Equal("public-test", first.ClientConfiguration["publicKey"]);
        Assert.Equal("widget-test", first.ClientConfiguration["widgetId"]);
        Assert.Single(
            unitOfWork.KycVerificationRepository.Items);
        Assert.Single(
            unitOfWork.KycVerificationAttemptRepository.Items);
    }

    [Fact]
    public async Task GenericDojahRetryCreatesFreshWidgetReferenceAndPreservesHistory()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = CreateActualDojahService(unitOfWork);
        var initial = await service.CreateSessionAsync(Guid.NewGuid());
        var kyc = Assert.Single(unitOfWork.KycVerificationRepository.Items);
        var historicalAttempt = Assert.Single(
            unitOfWork.KycVerificationAttemptRepository.Items);
        kyc.Status = KycStatus.Rejected;
        kyc.ProviderStatus = "Failed";
        kyc.RejectionReason = "Verification failed.";
        historicalAttempt.Status = KycProviderStatus.Rejected;
        historicalAttempt.RawStatus = "Failed";

        var retry = await service.RetryAsync(kyc.UserId);

        Assert.Equal("Dojah", retry.Provider);
        Assert.Equal("Widget", retry.IntegrationType);
        Assert.StartsWith("PRYDE-", retry.Reference);
        Assert.NotEqual(initial.Reference, retry.Reference);
        Assert.NotEqual(initial.SessionUrl, retry.SessionUrl);
        Assert.NotNull(retry.SessionUrl);
        Assert.Contains(retry.Reference, retry.SessionUrl);
        Assert.DoesNotContain(
            "SMILE-GROUP-",
            retry.SessionUrl,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            2,
            unitOfWork.KycVerificationAttemptRepository.Items.Count);
        Assert.Contains(
            historicalAttempt,
            unitOfWork.KycVerificationAttemptRepository.Items);
        Assert.Equal(KycProviderStatus.Rejected, historicalAttempt.Status);
    }

    [Fact]
    public async Task ApprovedDojahUserReceivesTerminalResultWithoutNewWidgetSession()
    {
        var unitOfWork = new TestUnitOfWork();
        var kyc = PendingKyc("Dojah", "PRYDE-approved");
        kyc.Status = KycStatus.Approved;
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        var service = CreateActualDojahService(unitOfWork);

        var result = await service.CreateSessionAsync(kyc.UserId);

        Assert.Equal("Dojah", result.Provider);
        Assert.Equal(KycProviderStatus.Approved, result.Status);
        Assert.Null(result.IntegrationType);
        Assert.Null(result.SessionUrl);
        Assert.Empty(result.ClientConfiguration);
        Assert.Empty(result.Sessions);
        Assert.Empty(
            unitOfWork.KycVerificationAttemptRepository.Items);
    }

    [Fact]
    public void DojahHostedRedirectIntegrationTypeIsRejected()
    {
        var result = new KycProviderResult
        {
            Provider = "Dojah",
            IntegrationType = "HostedRedirect",
            Reference = "PRYDE-valid",
            SessionUrl = "https://identity.dojah.io/session",
            Status = KycProviderStatus.Pending,
            ClientConfiguration = ValidDojahClientConfiguration()
        };

        Assert.Throws<ServiceUnavailableException>(() =>
            KycProviderResultInvariant.Ensure("Dojah", result));
    }

    [Fact]
    public void SmileWidgetIntegrationTypeIsRejected()
    {
        var result = new KycProviderResult
        {
            Provider = "SmileId",
            IntegrationType = "Widget",
            Reference = "SMILE-GROUP-valid",
            Status = KycProviderStatus.Pending
        };

        Assert.Throws<ServiceUnavailableException>(() =>
            KycProviderResultInvariant.Ensure("SmileId", result));
    }

    [Theory]
    [InlineData("Dojah", "PRYDE-valid", "https://identity.dojah.io/session")]
    [InlineData("SmileId", "SMILE-GROUP-valid", null)]
    public void ActionableSessionWithNullIntegrationTypeIsRejected(
        string provider,
        string reference,
        string? sessionUrl)
    {
        var result = new KycProviderResult
        {
            Provider = provider,
            Reference = reference,
            SessionUrl = sessionUrl,
            Status = KycProviderStatus.Pending,
            ClientConfiguration = provider == "Dojah"
                ? ValidDojahClientConfiguration()
                : new Dictionary<string, string>(),
            Sessions = provider == "SmileId"
                ?
                [
                    new KycProviderSession
                    {
                        Flow = "IdentityVerification",
                        JobId = "PRYDE-SMILE-job",
                        VerificationUrl = "https://links.usesmileid.com/test",
                        Required = true,
                        Status = "Pending"
                    }
                ]
                : []
        };

        Assert.Throws<ServiceUnavailableException>(() =>
            KycProviderResultInvariant.Ensure(provider, result));
    }

    [Fact]
    public void SmileResponseWithDojahReferenceIsRejected()
    {
        var result = new KycProviderResult
        {
            Provider = "SmileId",
            IntegrationType = "HostedRedirect",
            Reference = "PRYDE-dojah-reference",
            Status = KycProviderStatus.Pending
        };

        Assert.Throws<ServiceUnavailableException>(() =>
            KycProviderResultInvariant.Ensure("SmileId", result));
    }

    [Fact]
    public async Task ExistingDojahRecordKeepsReferencesAndGainsAttemptHistory()
    {
        var unitOfWork = new TestUnitOfWork();
        var kyc = new KycVerification
        {
            UserId = Guid.NewGuid(),
            Status = KycStatus.Pending,
            ProviderName = "Dojah",
            ProviderReference = "PRYDE-existing",
            DojahReference = "DJ-existing",
            ProviderStatus = "Ongoing"
        };
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        var service = new DojahKycService(
            unitOfWork,
            Options.Create(DojahSettings()),
            NullLogger<DojahKycService>.Instance);

        var result = await service.GetConfigAsync(kyc.UserId);

        Assert.Equal("PRYDE-existing", result.ProviderReference);
        Assert.Equal("DJ-existing", kyc.DojahReference);
        var attempt = Assert.Single(
            unitOfWork.KycVerificationAttemptRepository.Items);
        Assert.Equal(kyc.Id, attempt.KycVerificationId);
        Assert.Equal("PRYDE-existing", attempt.CorrelationReference);
        Assert.Equal("DJ-existing", attempt.ProviderReference);
    }

    [Fact]
    public async Task MissingSmileIdConfigurationDoesNotAffectStartup()
    {
        var values = new Dictionary<string, string?>
        {
            ["Kyc:ActiveProvider"] = "Dojah",
            ["Dojah:Enabled"] = "true",
            ["Dojah:BaseUrl"] = "https://api.dojah.io",
            ["Dojah:AppId"] = "app-test",
            ["Dojah:ApiToken"] = "api-token",
            ["Dojah:PublicKey"] = "public-test",
            ["Dojah:PrivateKey"] = "private-test",
            ["Dojah:ShareableLink"] =
                "https://identity.dojah.io/?widget_id=widget-test"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var builder = Host.CreateApplicationBuilder();
        var unitOfWork = new TestUnitOfWork();
        builder.Services.AddSingleton<IUnitOfWork>(unitOfWork);
        builder.Services.AddSingleton<INotificationService>(
            new NotificationService(unitOfWork));
        builder.Services.AddDojahIntegration(configuration);
        using var host = builder.Build();

        await host.StartAsync();
        var active = host.Services.GetRequiredService<IKycProviderResolver>()
            .ResolveActive();

        Assert.Equal("Dojah", active.Name);
        await host.StopAsync();
    }

    private static DojahSettings DojahSettings() => new()
    {
        Enabled = true,
        BaseUrl = "https://api.dojah.io",
        AppId = "app-test",
        ApiToken = "api-token",
        PublicKey = "public-test",
        PrivateKey = "private-test",
        ShareableLink = "https://identity.dojah.io/?widget_id=widget-test"
    };

    private static KycProviderService CreateService(
        TestUnitOfWork unitOfWork,
        string activeProvider,
        params RecordingProvider[] providers) =>
        new(
            new KycProviderResolver(
                providers,
                Options.Create(new KycSettings
                {
                    ActiveProvider = activeProvider
                })),
            unitOfWork,
            NullLogger<KycProviderService>.Instance);

    private static KycProviderService CreateActualDojahService(
        TestUnitOfWork unitOfWork)
    {
        var kycOptions = Options.Create(new KycSettings
        {
            ActiveProvider = "Dojah"
        });
        var dojah = new DojahKycProvider(
            unitOfWork,
            Options.Create(DojahSettings()),
            NullLogger<DojahKycProvider>.Instance,
            new NotificationService(unitOfWork),
            kycOptions);
        return new KycProviderService(
            new KycProviderResolver(
                [dojah, new RecordingProvider("SmileId")],
                kycOptions),
            unitOfWork,
            NullLogger<KycProviderService>.Instance);
    }

    private static KycVerification PendingKyc(
        string provider,
        string reference) =>
        new()
        {
            UserId = Guid.NewGuid(),
            ProviderName = provider,
            ProviderReference = reference,
            Status = KycStatus.Pending
        };

    private static IReadOnlyDictionary<string, string>
        ValidDojahClientConfiguration() =>
        new Dictionary<string, string>
        {
            ["appId"] = "app-test",
            ["publicKey"] = "public-test",
            ["widgetId"] = "widget-test"
        };

    private sealed class RecordingProvider(
        string name,
        Func<KycProviderResult>? resultFactory = null) : IKycProvider
    {
        public string Name { get; } = name;
        public List<Guid> UserIds { get; } = [];

        public Task<KycProviderResult> CreateSessionAsync(
            KycProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            UserIds.Add(request.UserId);
            return Task.FromResult(Result());
        }

        public Task<KycProviderResult> RetryAsync(
            KycProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            UserIds.Add(request.UserId);
            return Task.FromResult(Result());
        }

        private KycProviderResult Result() => resultFactory?.Invoke() ??
            (Name == "SmileId"
                ? new KycProviderResult
                {
                    Provider = Name,
                    IntegrationType = "HostedRedirect",
                    Reference = "SMILE-GROUP-new",
                    Status = KycProviderStatus.Pending,
                    Sessions =
                    [
                        new KycProviderSession
                        {
                            Flow = "IdentityVerification",
                            JobId = $"PRYDE-SMILE-{Guid.NewGuid():N}",
                            VerificationUrl = "https://links.usesmileid.com/test",
                            Required = true,
                            Status = "Pending"
                        }
                    ]
                }
                : new KycProviderResult
                {
                    Provider = Name,
                    IntegrationType = "Widget",
                    Reference = $"PRYDE-{Guid.NewGuid():N}",
                    SessionUrl = "https://identity.dojah.io/session",
                    Status = KycProviderStatus.Pending,
                    ClientConfiguration = new Dictionary<string, string>
                    {
                        ["appId"] = "app-test",
                        ["publicKey"] = "public-test",
                        ["widgetId"] = "widget-test"
                    }
                });
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
