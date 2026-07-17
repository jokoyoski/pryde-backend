using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Settings;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class DojahKycServiceTests
{
    [Fact]
    public void SettingsBindFromDojahSection()
    {
        var values = new Dictionary<string, string?>
        {
            ["Dojah:Enabled"] = "true",
            ["Dojah:AppId"] = "bound-app",
            ["Dojah:PublicKey"] = "bound-public"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var settings = configuration.GetSection(DojahSettings.SectionName).Get<DojahSettings>();

        Assert.NotNull(settings);
        Assert.True(settings.Enabled);
        Assert.Equal("bound-app", settings.AppId);
        Assert.Equal("bound-public", settings.PublicKey);
    }

    [Fact]
    public void DisabledSettingsAreValidAndDoNotRequireSecrets()
    {
        var result = new DojahSettingsValidator().Validate(null, new DojahSettings());

        Assert.False(result.Failed);
    }

    [Fact]
    public void EnabledSettingsFailClearlyWhenRequiredValuesAreMissing()
    {
        var result = new DojahSettingsValidator().Validate(null, new DojahSettings { Enabled = true });

        Assert.True(result.Failed);
        Assert.Contains(nameof(DojahSettings.AppId), result.FailureMessage);
        Assert.Contains(nameof(DojahSettings.PrivateKey), result.FailureMessage);
    }

    [Fact]
    public async Task AuthenticatedUserGetsSafeConfigurationWithStableCorrelationReference()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var userId = Guid.NewGuid();

        var first = await service.GetConfigAsync(userId);
        var second = await service.GetConfigAsync(userId);

        Assert.Equal(first.ReferenceId, second.ReferenceId);
        Assert.StartsWith("PRYDE-", first.ReferenceId);
        Assert.Equal("widget-test", first.WidgetId);
        Assert.Equal(KycStatus.Pending, first.Status);
        Assert.Single(unitOfWork.KycVerificationRepository.Items);
    }

    [Fact]
    public void PublicConfigurationContractDoesNotContainSecrets()
    {
        var propertyNames = typeof(DojahKycConfigResponseDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain(nameof(DojahSettings.ApiToken), propertyNames);
        Assert.DoesNotContain(nameof(DojahSettings.PrivateKey), propertyNames);
    }

    [Fact]
    public async Task DisabledDojahReturnsServiceUnavailableWithoutChangingKyc()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = new DojahKycService(unitOfWork, Options.Create(new DojahSettings()));

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.GetConfigAsync(Guid.NewGuid()));
        Assert.Empty(unitOfWork.KycVerificationRepository.Items);
    }

    [Theory]
    [InlineData("Completed", KycStatus.Approved)]
    [InlineData("Failed", KycStatus.Rejected)]
    [InlineData("Ongoing", KycStatus.Pending)]
    [InlineData("Pending", KycStatus.Pending)]
    [InlineData("Abandoned", KycStatus.Pending)]
    [InlineData("FutureStatus", KycStatus.Pending)]
    public async Task VerifiedWebhookMapsProviderStatus(string providerStatus, KycStatus expectedStatus)
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ReferenceId, providerStatus, "  provider   check failed  ");

        await service.ProcessWebhookAsync(payload, Sign(payload));

        var kyc = Assert.Single(unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(expectedStatus, kyc.Status);
        Assert.Equal(providerStatus, kyc.ProviderStatus);
        if (expectedStatus == KycStatus.Rejected)
            Assert.Equal("provider check failed", kyc.RejectionReason);
    }

    [Fact]
    public async Task InvalidWebhookSignatureIsRejectedWithoutChangingStatus()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ReferenceId, "Completed");

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.ProcessWebhookAsync(payload, new string('0', 64)));

        Assert.Equal(KycStatus.Pending, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    [Fact]
    public async Task DuplicateWebhookIsIdempotent()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ReferenceId, "Completed");
        var signature = Sign(payload);

        await service.ProcessWebhookAsync(payload, signature);
        var saveCount = unitOfWork.SaveChangesCount;
        await service.ProcessWebhookAsync(payload, signature);

        Assert.Equal(saveCount, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task OldProviderCallbackCannotDowngradeApprovedKyc()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var completed = Payload(config.ReferenceId, "Completed");
        await service.ProcessWebhookAsync(completed, Sign(completed));
        var oldPending = Payload(config.ReferenceId, "Ongoing");

        await service.ProcessWebhookAsync(oldPending, Sign(oldPending));

        Assert.Equal(KycStatus.Approved, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    private static DojahKycService Service(TestUnitOfWork unitOfWork) =>
        new(unitOfWork, Options.Create(Settings()));

    private static DojahSettings Settings() => new()
    {
        Enabled = true,
        AppId = "app-test",
        PublicKey = "public-test",
        PrivateKey = "private-test",
        ShareableLink = "https://identity.dojah.io/?widget_id=widget-test"
    };

    private static byte[] Payload(string referenceId, string status, string message = "") =>
        Encoding.UTF8.GetBytes($"{{\"reference_id\":\"{referenceId}\",\"verification_status\":\"{status}\",\"message\":\"{message}\"}}");

    private static string Sign(byte[] payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Settings().PrivateKey));
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }
}
