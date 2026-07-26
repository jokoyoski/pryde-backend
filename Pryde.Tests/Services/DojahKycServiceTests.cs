using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
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
            ["Dojah:PublicKey"] = "bound-public",
            ["Dojah:PrivateKey"] = "bound-private"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var settings = configuration.GetSection(DojahSettings.SectionName).Get<DojahSettings>();

        Assert.NotNull(settings);
        Assert.True(settings.Enabled);
        Assert.Equal("bound-app", settings.AppId);
        Assert.Equal("bound-public", settings.PublicKey);
        Assert.Equal("bound-private", settings.PrivateKey);
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
        Assert.Equal(first.ReferenceId, first.Metadata["kyc_reference"]);
        Assert.Contains(
            $"reference_id={first.ReferenceId}",
            first.ShareableLink);
        Assert.Contains(
            $"metadata%5Bkyc_reference%5D={first.ReferenceId}",
            first.ShareableLink);
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
        var service = new DojahKycService(
            unitOfWork,
            Options.Create(new DojahSettings()),
            NullLogger<DojahKycService>.Instance);

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

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        var kyc = Assert.Single(unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(expectedStatus, kyc.Status);
        Assert.Equal(providerStatus, kyc.ProviderStatus);
        if (expectedStatus == KycStatus.Rejected)
            Assert.Equal("provider check failed", kyc.RejectionReason);
    }

    [Fact]
    public async Task WebhookUsingCustomPrydeReferenceUpdatesVerification()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ReferenceId, "Completed");

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        var kyc = Assert.Single(unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(config.ReferenceId, kyc.ProviderReference);
        Assert.Null(kyc.DojahReference);
        Assert.Equal("Completed", kyc.ProviderStatus);
        Assert.Equal(KycStatus.Approved, kyc.Status);
        Assert.NotNull(kyc.VerifiedAt);
        Assert.NotNull(kyc.LastProviderUpdatedAt);
    }

    [Fact]
    public async Task WebhookUsingDojahReferenceAndCustomMetadataStoresBothReferences()
    {
        const string dojahReference = "DJ-31038041E0";
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = PayloadWithMetadata(
            dojahReference,
            config.ReferenceId,
            "Completed");

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        var kyc = Assert.Single(unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(config.ReferenceId, kyc.ProviderReference);
        Assert.Equal(dojahReference, kyc.DojahReference);
        Assert.Equal(KycStatus.Approved, kyc.Status);
    }

    [Fact]
    public async Task LaterWebhookCanResolveStoredDojahReferenceWithoutMetadata()
    {
        const string dojahReference = "DJ-31038041E1";
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var firstPayload = PayloadWithMetadata(
            dojahReference,
            config.ReferenceId,
            "Ongoing");
        await service.ProcessWebhookAsync(
            firstPayload,
            SignV1(firstPayload),
            null);
        var completedPayload = Payload(dojahReference, "Completed");

        await service.ProcessWebhookAsync(
            completedPayload,
            SignV1(completedPayload),
            null);

        var kyc = Assert.Single(unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(dojahReference, kyc.DojahReference);
        Assert.Equal(KycStatus.Approved, kyc.Status);
    }

    [Fact]
    public async Task UnknownDojahReferenceIsRejected()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var payload = Payload("DJ-UNKNOWN001", "Completed");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.ProcessWebhookAsync(payload, SignV1(payload), null));

        Assert.Empty(unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task DuplicateDojahWebhookIsIdempotent()
    {
        const string dojahReference = "DJ-31038041E2";
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = PayloadWithMetadata(
            dojahReference,
            config.ReferenceId,
            "Completed");
        var signature = SignV1(payload);

        await service.ProcessWebhookAsync(payload, signature, null);
        var saveCount = unitOfWork.SaveChangesCount;
        var updatedAt = unitOfWork.KycVerificationRepository.Items[0]
            .LastProviderUpdatedAt;
        await service.ProcessWebhookAsync(payload, signature, null);

        Assert.Equal(saveCount, unitOfWork.SaveChangesCount);
        Assert.Equal(
            updatedAt,
            unitOfWork.KycVerificationRepository.Items[0].LastProviderUpdatedAt);
    }

    [Fact]
    public async Task FailedDojahWebhookSetsRejectedDecisionFields()
    {
        const string dojahReference = "DJ-31038041E3";
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = PayloadWithMetadata(
            dojahReference,
            config.ReferenceId,
            "Failed",
            "Document could not be verified.");

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        var kyc = Assert.Single(unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(KycStatus.Rejected, kyc.Status);
        Assert.Equal("Failed", kyc.ProviderStatus);
        Assert.Equal(
            "Document could not be verified.",
            kyc.RejectionReason);
        Assert.Null(kyc.VerifiedAt);
        Assert.NotNull(kyc.LastProviderUpdatedAt);
    }

    [Fact]
    public async Task ValidV1SignatureIsAccepted()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ReferenceId, "Completed");

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        Assert.Equal(KycStatus.Approved, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    [Fact]
    public async Task ValidV2SignatureIsAccepted()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ReferenceId, "Completed");

        await service.ProcessWebhookAsync(payload, null, SignV2());

        Assert.Equal(KycStatus.Approved, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    [Fact]
    public async Task InvalidV1SignatureIsRejectedWithoutChangingStatus()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ReferenceId, "Completed");

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.ProcessWebhookAsync(payload, new string('0', 64), null));

        Assert.Equal(KycStatus.Pending, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    [Fact]
    public async Task InvalidV2SignatureIsRejectedWithoutChangingStatus()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ReferenceId, "Completed");

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.ProcessWebhookAsync(payload, null, new string('0', 64)));

        Assert.Equal(KycStatus.Pending, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    [Fact]
    public async Task V2IsPreferredWhenBothSignatureHeadersArePresent()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ReferenceId, "Completed");

        await service.ProcessWebhookAsync(payload, new string('0', 64), SignV2());

        Assert.Equal(KycStatus.Approved, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    [Fact]
    public async Task NeitherSignatureHeaderIsRejected()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ReferenceId, "Completed");

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.ProcessWebhookAsync(payload, null, null));

        Assert.Equal(KycStatus.Pending, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    [Fact]
    public async Task SignatureWithSha256PrefixAndWhitespaceIsAccepted()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ReferenceId, "Completed");

        await service.ProcessWebhookAsync(
            payload,
            $"  sha256={SignV1(payload).ToUpperInvariant()}  ",
            null);

        Assert.Equal(KycStatus.Approved, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    [Fact]
    public async Task SignatureIsCalculatedFromExactPayloadBytes()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Encoding.UTF8.GetBytes(
            $"{{\n  \"reference_id\": \"{config.ReferenceId}\",\n  \"verification_status\": \"Completed\"\n}}");

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        Assert.Equal(KycStatus.Approved, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    [Fact]
    public async Task DuplicateWebhookIsIdempotent()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ReferenceId, "Completed");
        var signature = SignV1(payload);

        await service.ProcessWebhookAsync(payload, signature, null);
        var saveCount = unitOfWork.SaveChangesCount;
        await service.ProcessWebhookAsync(payload, signature, null);

        Assert.Equal(saveCount, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task OldProviderCallbackCannotDowngradeApprovedKyc()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var completed = Payload(config.ReferenceId, "Completed");
        await service.ProcessWebhookAsync(completed, SignV1(completed), null);
        var oldPending = Payload(config.ReferenceId, "Ongoing");

        await service.ProcessWebhookAsync(oldPending, SignV1(oldPending), null);

        Assert.Equal(KycStatus.Approved, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    private static DojahKycService Service(TestUnitOfWork unitOfWork) =>
        new(unitOfWork, Options.Create(Settings()), NullLogger<DojahKycService>.Instance);

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

    private static byte[] PayloadWithMetadata(
        string dojahReference,
        string customReference,
        string status,
        string message = "") =>
        Encoding.UTF8.GetBytes(
            $"{{\"reference_id\":\"{dojahReference}\",\"verification_status\":\"{status}\",\"message\":\"{message}\",\"metadata\":{{\"kyc_reference\":\"{customReference}\"}}}}");

    private static string SignV1(byte[] payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Settings().PrivateKey));
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }

    private static string SignV2() =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Settings().PrivateKey)))
            .ToLowerInvariant();
}
