using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        var service = new KycProviderService(resolver);
        var userId = Guid.NewGuid();

        var session = await service.CreateSessionAsync(userId);
        var retry = await service.RetryAsync(userId);

        Assert.Equal("Dojah", session.Provider);
        Assert.Equal("Dojah", retry.Provider);
        Assert.Equal([userId, userId], dojah.UserIds);
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

    private sealed class RecordingProvider(string name) : IKycProvider
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

        private KycProviderResult Result() => new()
        {
            Provider = Name,
            Reference = "reference",
            Status = KycProviderStatus.Pending
        };
    }
}
