using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Constants;
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
            ["Dojah:BaseUrl"] = "https://sandbox.dojah.io",
            ["Dojah:Enabled"] = "true",
            ["Dojah:AppId"] = "bound-app",
            ["Dojah:ApiKey"] = "obsolete-api-key",
            ["Dojah:PublicKey"] = "bound-public",
            ["Dojah:PrivateKey"] = "bound-private",
            ["DojahSettings:PrivateKey"] = "wrong-section-private"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var settings = configuration.GetSection(DojahSettings.SectionName).Get<DojahSettings>();

        Assert.NotNull(settings);
        Assert.True(settings.Enabled);
        Assert.Equal("https://sandbox.dojah.io", settings.BaseUrl);
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
        Assert.Contains(nameof(DojahSettings.BaseUrl), result.FailureMessage);
        Assert.Contains(nameof(DojahSettings.AppId), result.FailureMessage);
        Assert.Contains(nameof(DojahSettings.PrivateKey), result.FailureMessage);
    }

    [Fact]
    public void EnabledSettingsUsePrivateKeyAndDoNotRequireApiToken()
    {
        var settings = Settings();
        settings.ApiToken = string.Empty;

        var result = new DojahSettingsValidator().Validate(null, settings);

        Assert.False(result.Failed);
    }

    [Fact]
    public void EnabledSettingsRejectNonHttpsBaseUrl()
    {
        var settings = Settings();
        settings.BaseUrl = "http://api.dojah.io";
        settings.ApiToken = "api-token";

        var result = new DojahSettingsValidator().Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains("BaseUrl", result.FailureMessage);
        Assert.Contains("HTTPS", result.FailureMessage);
    }

    [Fact]
    public void EnabledSettingsRejectLeadingOrTrailingWhitespace()
    {
        var settings = Settings();
        settings.PrivateKey = $" {settings.PrivateKey}";

        var result = new DojahSettingsValidator().Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains(nameof(DojahSettings.PrivateKey), result.FailureMessage);
        Assert.Contains("whitespace", result.FailureMessage);
    }

    [Fact]
    public void EnabledSettingsRejectCredentialValuesThatMustBeDifferent()
    {
        var validator = new DojahSettingsValidator();
        var appIdMatchesPrivateKey = Settings();
        appIdMatchesPrivateKey.PrivateKey = appIdMatchesPrivateKey.AppId;
        var publicKeyMatchesPrivateKey = Settings();
        publicKeyMatchesPrivateKey.PrivateKey = publicKeyMatchesPrivateKey.PublicKey;
        var appIdMatchesApiToken = Settings();
        appIdMatchesApiToken.ApiToken = appIdMatchesApiToken.AppId;

        var appIdResult = validator.Validate(null, appIdMatchesPrivateKey);
        var publicKeyResult = validator.Validate(
            null,
            publicKeyMatchesPrivateKey);
        var apiTokenResult = validator.Validate(null, appIdMatchesApiToken);

        Assert.True(appIdResult.Failed);
        Assert.Contains("AppId and PrivateKey", appIdResult.FailureMessage);
        Assert.True(publicKeyResult.Failed);
        Assert.Contains("PublicKey and PrivateKey", publicKeyResult.FailureMessage);
        Assert.True(apiTokenResult.Failed);
        Assert.Contains("AppId and ApiToken", apiTokenResult.FailureMessage);
    }

    [Fact]
    public async Task AuthenticatedUserGetsSafeConfigurationWithStableCorrelationReference()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var userId = Guid.NewGuid();

        var first = await service.GetConfigAsync(userId);
        var second = await service.GetConfigAsync(userId);

        Assert.NotNull(first.ReferenceId);
        Assert.Equal(first.ProviderReference, first.ReferenceId);
        Assert.Equal(second.ProviderReference, second.ReferenceId);
        Assert.Equal(first.ProviderReference, second.ProviderReference);
        Assert.StartsWith("PRYDE-", first.ReferenceId);
        Assert.StartsWith("PRYDE-", first.ProviderReference);
        Assert.Equal(
            first.ProviderReference,
            first.Metadata["kyc_reference"]);
        Assert.Contains(
            $"reference_id={first.ProviderReference}",
            first.ShareableLink,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"metadata%5Bkyc_reference%5D={first.ProviderReference}",
            first.ShareableLink);
        Assert.Equal("app-test", first.AppId);
        Assert.Equal("public-test", first.PublicKey);
        Assert.Equal("widget-test", first.WidgetId);
        Assert.Equal(KycStatus.Pending, first.Status);
        Assert.Single(unitOfWork.KycVerificationRepository.Items);
    }

    [Fact]
    public async Task RejectedUserCanRetry()
    {
        var unitOfWork = RejectedKycContext(out var kyc);
        var service = Service(unitOfWork);

        var result = await service.RetryAsync(kyc.UserId);

        Assert.Equal(KycStatus.Pending, result.Status);
        Assert.Equal(KycStatus.Pending, kyc.Status);
        Assert.Equal("Dojah", kyc.ProviderName);
        var attempts = unitOfWork.KycVerificationAttemptRepository.Items;
        Assert.Equal(2, attempts.Count);
        Assert.Contains(attempts, attempt =>
            attempt.CorrelationReference == "PRYDE-old-rejected-attempt" &&
            attempt.Status == KycProviderStatus.Rejected);
        Assert.Contains(attempts, attempt =>
            attempt.CorrelationReference == result.ProviderReference &&
            attempt.Status == KycProviderStatus.Pending);
    }

    [Fact]
    public async Task RetryGeneratesDifferentReference()
    {
        var unitOfWork = RejectedKycContext(out var kyc);
        var previousReference = kyc.ProviderReference;
        var service = Service(unitOfWork);
        var previousConfig = await service.GetConfigAsync(kyc.UserId);

        var result = await service.RetryAsync(kyc.UserId);

        Assert.NotEqual(previousReference, result.ProviderReference);
        Assert.StartsWith("PRYDE-", result.ProviderReference);
        Assert.Equal(result.ProviderReference, result.ReferenceId);
        Assert.NotEqual(previousReference, result.ReferenceId);
        Assert.NotEqual(previousConfig.ShareableLink, result.ShareableLink);
        Assert.DoesNotContain(previousReference!, result.ShareableLink);
        Assert.Equal(result.ProviderReference, kyc.ProviderReference);
        Assert.Equal(
            result.ProviderReference,
            result.Metadata["kyc_reference"]);
    }

    [Fact]
    public async Task RetryClearsRejectionReasonAndPreviousProviderResult()
    {
        var unitOfWork = RejectedKycContext(out var kyc);
        var service = Service(unitOfWork);

        await service.RetryAsync(kyc.UserId);

        Assert.Null(kyc.RejectionReason);
        Assert.Null(kyc.DojahReference);
        Assert.Null(kyc.ProviderStatus);
        Assert.Null(kyc.LastProviderUpdatedAt);
        Assert.Null(kyc.VerifiedAt);
    }

    [Fact]
    public async Task ApprovedUserCannotRetry()
    {
        var unitOfWork = RejectedKycContext(out var kyc);
        kyc.Status = KycStatus.Approved;
        var service = Service(unitOfWork);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RetryAsync(kyc.UserId));
    }

    [Fact]
    public async Task PendingUserCannotRetry()
    {
        var unitOfWork = RejectedKycContext(out var kyc);
        kyc.Status = KycStatus.Pending;
        var service = Service(unitOfWork);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RetryAsync(kyc.UserId));
    }

    [Fact]
    public async Task SubmittedUserCannotRetry()
    {
        var unitOfWork = RejectedKycContext(out var kyc);
        kyc.Status = KycStatus.Submitted;
        var service = Service(unitOfWork);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RetryAsync(kyc.UserId));
    }

    [Fact]
    public async Task SecondRetryWhilePendingIsRejected()
    {
        var unitOfWork = RejectedKycContext(out var kyc);
        var service = Service(unitOfWork);

        await service.RetryAsync(kyc.UserId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RetryAsync(kyc.UserId));
    }

    [Fact]
    public async Task ConcurrentRetryRequestsAllowOnlyOneNewAttempt()
    {
        var unitOfWork = RejectedKycContext(out var kyc);
        var service = Service(unitOfWork);
        var retryTasks = new[]
        {
            service.RetryAsync(kyc.UserId),
            service.RetryAsync(kyc.UserId)
        };
        var successes = 0;
        var conflicts = 0;

        foreach (var retryTask in retryTasks)
        {
            try
            {
                await retryTask;
                successes++;
            }
            catch (ConflictException)
            {
                conflicts++;
            }
        }

        Assert.Equal(1, successes);
        Assert.Equal(1, conflicts);
        Assert.Equal(KycStatus.Pending, kyc.Status);
    }

    [Fact]
    public async Task RetryUpdatesOnlyTheAuthenticatedUsersKyc()
    {
        var unitOfWork = RejectedKycContext(out var kyc);
        var otherKyc = new KycVerification
        {
            UserId = Guid.NewGuid(),
            Status = KycStatus.Rejected,
            ProviderName = "Dojah",
            ProviderReference = "PRYDE-other-rejected-attempt",
            DojahReference = "provider-generated-other-reference",
            ProviderStatus = "Failed",
            RejectionReason = "Other rejection."
        };
        unitOfWork.KycVerificationRepository.Items.Add(otherKyc);
        var service = Service(unitOfWork);

        await service.RetryAsync(kyc.UserId);

        Assert.Equal(KycStatus.Pending, kyc.Status);
        Assert.Equal(KycStatus.Rejected, otherKyc.Status);
        Assert.Equal("PRYDE-other-rejected-attempt", otherKyc.ProviderReference);
        Assert.Equal("provider-generated-other-reference", otherKyc.DojahReference);
        Assert.Equal("Other rejection.", otherKyc.RejectionReason);
    }

    [Fact]
    public async Task OldWebhookReferenceCannotUpdateRetryAttempt()
    {
        var unitOfWork = RejectedKycContext(out var kyc);
        var oldProviderReference = kyc.ProviderReference!;
        var oldDojahReference = kyc.DojahReference!;
        var service = Service(unitOfWork);
        await service.RetryAsync(kyc.UserId);
        var payload = PayloadWithMetadata(
            oldDojahReference,
            oldProviderReference,
            "Completed");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.ProcessWebhookAsync(payload, SignV1(payload), null));

        Assert.Equal(KycStatus.Pending, kyc.Status);
        Assert.NotEqual(oldProviderReference, kyc.ProviderReference);
        Assert.Null(kyc.DojahReference);
    }

    [Fact]
    public async Task NewWebhookReferenceCanUpdateRetryAttempt()
    {
        const string newDojahReference = "provider-generated-retry-reference";
        var unitOfWork = RejectedKycContext(out var kyc);
        var service = Service(unitOfWork);
        var config = await service.RetryAsync(kyc.UserId);
        var payload = PayloadWithMetadata(
            newDojahReference,
            config.ProviderReference,
            "Completed");

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        Assert.Equal(KycStatus.Approved, kyc.Status);
        Assert.Equal(newDojahReference, kyc.DojahReference);
        Assert.Equal(config.ProviderReference, kyc.ProviderReference);
    }

    [Fact]
    public async Task NewFailedWebhookReferenceRejectsRetryAttempt()
    {
        const string newDojahReference = "provider-generated-failed-retry-reference";
        var unitOfWork = RejectedKycContext(out var kyc);
        var service = Service(unitOfWork);
        var config = await service.RetryAsync(kyc.UserId);
        var payload = PayloadWithMetadata(
            newDojahReference,
            config.ProviderReference,
            "Failed",
            "Retry identity check failed.");

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        Assert.Equal(KycStatus.Rejected, kyc.Status);
        Assert.Equal(newDojahReference, kyc.DojahReference);
        Assert.Equal("Failed", kyc.ProviderStatus);
        Assert.Equal("Retry identity check failed.", kyc.RejectionReason);
    }

    [Fact]
    public async Task RetryReturnsUserToIdentityVerificationOnboardingStage()
    {
        var unitOfWork = RejectedKycContext(out var kyc);
        ((TestUserRepository)unitOfWork.Users).Items.Add(new User
        {
            Id = kyc.UserId,
            Email = "retry@test.local",
            PhoneNumber = "08000000000",
            IsEmailVerified = true,
            Status = UserStatus.Active
        });
        unitOfWork.UserRoleRepository.Items.Add(new UserRole
        {
            UserId = kyc.UserId,
            Role = new Role { Name = RoleNames.Passenger }
        });
        var service = Service(unitOfWork);

        await service.RetryAsync(kyc.UserId);
        var onboarding = await new OnboardingStatusService(unitOfWork)
            .GetAsync(kyc.UserId);

        Assert.Equal(
            OnboardingStage.IdentityVerification,
            onboarding.CurrentStage);
        Assert.Equal(KycStatus.Pending, onboarding.KycStatus);
        Assert.Null(onboarding.RejectionReason);
    }

    [Fact]
    public async Task ShareableLinkReplacesOldCorrelationAndEncodesReferences()
    {
        const string providerReference = "PRYDE-value with/+characters";
        var unitOfWork = new TestUnitOfWork();
        unitOfWork.KycVerificationRepository.Items.Add(new KycVerification
        {
            UserId = Guid.Parse("74462e4a-98c5-4a1d-a480-ee5e45244a1e"),
            Status = KycStatus.Pending,
            ProviderReference = providerReference
        });
        var settings = Settings();
        settings.ShareableLink =
            "https://identity.dojah.io/?widget_id=widget-test" +
            "&reference_id=PRYDE-old" +
            "&metadata%5Bkyc_reference%5D=PRYDE-old" +
            "&return_url=https%3A%2F%2Fpryde.test%2Fcomplete";
        var service = Service(unitOfWork, settings);

        var result = await service.GetConfigAsync(
            Guid.Parse("74462e4a-98c5-4a1d-a480-ee5e45244a1e"));

        Assert.Contains(
            "reference_id=PRYDE-value%20with%2F%2Bcharacters",
            result.ShareableLink,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRYDE-old", result.ShareableLink);
        Assert.Contains(
            "metadata%5Bkyc_reference%5D=" +
            "PRYDE-value%20with%2F%2Bcharacters",
            result.ShareableLink);
        Assert.Contains(
            "return_url=https%3A%2F%2Fpryde.test%2Fcomplete",
            result.ShareableLink);
        Assert.Equal(providerReference, result.ProviderReference);
        Assert.Equal(providerReference, result.ReferenceId);
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
    [InlineData("Abandoned", KycStatus.Rejected)]
    [InlineData("FutureStatus", KycStatus.Pending)]
    public async Task VerifiedWebhookMapsProviderStatus(string providerStatus, KycStatus expectedStatus)
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ProviderReference, providerStatus, "  provider   check failed  ");

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        var kyc = Assert.Single(unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(expectedStatus, kyc.Status);
        Assert.Equal(providerStatus, kyc.ProviderStatus);
        if (expectedStatus == KycStatus.Rejected)
        {
            Assert.Equal("provider check failed", kyc.RejectionReason);
        }
    }

    [Fact]
    public async Task WebhookUsingCustomPrydeReferenceUpdatesVerification()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ProviderReference, "Completed");

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        var kyc = Assert.Single(unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(config.ProviderReference, kyc.ProviderReference);
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
            config.ProviderReference,
            "Completed");

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        var kyc = Assert.Single(unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(config.ProviderReference, kyc.ProviderReference);
        Assert.Equal(dojahReference, kyc.DojahReference);
        Assert.Equal(KycStatus.Approved, kyc.Status);
    }

    [Theory]
    [InlineData("vendor_reference")]
    [InlineData("customer_reference")]
    [InlineData("custom_reference")]
    public async Task ExistingTopLevelCorrelationFallbacksRemainSupported(
        string correlationField)
    {
        const string dojahReference = "DJ-FALLBACK001";
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Encoding.UTF8.GetBytes(
            $"{{\"reference_id\":\"{dojahReference}\",\"verification_status\":\"Ongoing\",\"{correlationField}\":\"{config.ProviderReference}\"}}");

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        var kyc = Assert.Single(
            unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(config.ProviderReference, kyc.ProviderReference);
        Assert.Equal(dojahReference, kyc.DojahReference);
        Assert.Equal(KycStatus.Pending, kyc.Status);
    }

    [Fact]
    public async Task ExistingMetadataReferenceIdFallbackRemainsSupported()
    {
        const string dojahReference = "DJ-FALLBACK002";
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Encoding.UTF8.GetBytes(
            $"{{\"reference_id\":\"{dojahReference}\",\"verification_status\":\"Ongoing\",\"metadata\":{{\"reference_id\":\"{config.ProviderReference}\"}}}}");

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        var kyc = Assert.Single(
            unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(config.ProviderReference, kyc.ProviderReference);
        Assert.Equal(dojahReference, kyc.DojahReference);
        Assert.Equal(KycStatus.Pending, kyc.Status);
    }

    [Fact]
    public async Task ProductionEasyOnboardReferenceIsParsedExactlyAndCompletedIsApproved()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Encoding.UTF8.GetBytes($$"""
        {
          "referenceId": "{{config.ProviderReference}}",
          "widgetId": "production-widget-id",
          "verificationStatus": "Completed",
          "message": "Successfully completed the verification."
        }
        """);

        await service.ProcessWebhookAsync(
            payload,
            SignV1(payload),
            null);

        var kyc = Assert.Single(
            unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(config.ProviderReference, kyc.ProviderReference);
        Assert.DoesNotContain("widgetId", kyc.ProviderReference);
        Assert.Equal(KycStatus.Approved, kyc.Status);
        Assert.Equal("Completed", kyc.ProviderStatus);
    }

    [Theory]
    [InlineData("reference")]
    [InlineData("Reference")]
    public async Task WebhookReferenceCasingVariantsRemainCompatible(
        string referenceProperty)
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Encoding.UTF8.GetBytes(
            $"{{\"{referenceProperty}\":\"{config.ProviderReference}\",\"VerificationStatus\":\"Completed\"}}");

        await service.ProcessWebhookAsync(
            payload,
            SignV1(payload),
            null);

        var kyc = Assert.Single(
            unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(KycStatus.Approved, kyc.Status);
        Assert.Equal(config.ProviderReference, kyc.ProviderReference);
    }

    [Fact]
    public async Task RealisticCompletedWebhookPersistsSuccessfulDecision()
    {
        const string dojahReference = "DJ-1212364EF1";
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Encoding.UTF8.GetBytes($$"""
        {
          "data": {
            "id": {
              "status": true,
              "data": {
                "id_data": {
                  "first_name": "Test",
                  "last_name": "User",
                  "document_type": "Driver License"
                }
              }
            },
            "selfie": {
              "status": true
            },
            "government_data": {
              "status": true
            }
          },
          "status": true,
          "message": "Successfully completed the verification.",
          "metadata": {
            "kyc_reference": "{{config.ProviderReference}}",
            "device_info": "test-device",
            "ipinfo": {
              "country": "Nigeria"
            }
          },
          "reference_id": "{{dojahReference}}",
          "verification_mode": "LIVENESS",
          "verification_type": "DL_ID",
          "verification_status": "Completed"
        }
        """);
        var beforeProcessing = DateTime.UtcNow;

        await service.ProcessWebhookAsync(
            payload,
            SignV1(payload),
            null);

        var afterProcessing = DateTime.UtcNow;
        var kyc = Assert.Single(
            unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(config.ProviderReference, kyc.ProviderReference);
        Assert.Equal(dojahReference, kyc.DojahReference);
        Assert.NotEqual(kyc.ProviderReference, kyc.DojahReference);
        Assert.Equal("Completed", kyc.ProviderStatus);
        Assert.Equal(KycStatus.Approved, kyc.Status);
        Assert.Null(kyc.RejectionReason);
        Assert.InRange(
            kyc.VerifiedAt!.Value,
            beforeProcessing,
            afterProcessing);
        Assert.InRange(
            kyc.LastProviderUpdatedAt!.Value,
            beforeProcessing,
            afterProcessing);
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
            config.ProviderReference,
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
    public async Task LaterModernWebhookEnrichesLegacyApprovedKyc()
    {
        const string dojahReference = "provider-generated-reference-42";
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var legacyPayload = Payload(
            config.ProviderReference,
            "Completed");
        await service.ProcessWebhookAsync(
            legacyPayload,
            SignV1(legacyPayload),
            null);
        var modernPayload = PayloadWithMetadata(
            dojahReference,
            config.ProviderReference,
            "Completed");

        await service.ProcessWebhookAsync(
            modernPayload,
            SignV1(modernPayload),
            null);

        var kyc = Assert.Single(unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(dojahReference, kyc.DojahReference);
        Assert.Equal(KycStatus.Approved, kyc.Status);
    }

    [Fact]
    public async Task ExistingIdenticalDojahReferenceIsAccepted()
    {
        const string dojahReference = "provider-generated-reference-43";
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var ongoingPayload = PayloadWithMetadata(
            dojahReference,
            config.ProviderReference,
            "Ongoing");
        await service.ProcessWebhookAsync(
            ongoingPayload,
            SignV1(ongoingPayload),
            null);
        var completedPayload = PayloadWithMetadata(
            dojahReference,
            config.ProviderReference,
            "Completed");

        await service.ProcessWebhookAsync(
            completedPayload,
            SignV1(completedPayload),
            null);

        var kyc = Assert.Single(unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(dojahReference, kyc.DojahReference);
        Assert.Equal(KycStatus.Approved, kyc.Status);
    }

    [Fact]
    public async Task MismatchedExistingDojahReferenceIsRejectedWithoutUpdatingKyc()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var kyc = Assert.Single(unitOfWork.KycVerificationRepository.Items);
        kyc.DojahReference = "provider-generated-reference-original";
        var payload = PayloadWithMetadata(
            "provider-generated-reference-different",
            config.ProviderReference,
            "Completed");
        var saveCount = unitOfWork.SaveChangesCount;

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ProcessWebhookAsync(
                payload,
                SignV1(payload),
                null));

        Assert.Equal(
            "provider-generated-reference-original",
            kyc.DojahReference);
        Assert.Equal(KycStatus.Pending, kyc.Status);
        Assert.Equal(saveCount, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task MismatchedProviderReferenceIsRejectedWithoutUpdatingKyc()
    {
        const string dojahReference = "provider-generated-reference-44";
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var kyc = Assert.Single(unitOfWork.KycVerificationRepository.Items);
        kyc.DojahReference = dojahReference;
        var payload = PayloadWithMetadata(
            dojahReference,
            "PRYDE-different-correlation",
            "Completed");
        var saveCount = unitOfWork.SaveChangesCount;

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ProcessWebhookAsync(
                payload,
                SignV1(payload),
                null));

        Assert.Equal(KycStatus.Pending, kyc.Status);
        Assert.Equal(config.ProviderReference, kyc.ProviderReference);
        Assert.Equal(saveCount, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ModernWebhookDoesNotCreateDuplicateKycRecord()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = PayloadWithMetadata(
            "provider-generated-reference-45",
            config.ProviderReference,
            "Completed");

        await service.ProcessWebhookAsync(
            payload,
            SignV1(payload),
            null);

        Assert.Single(unitOfWork.KycVerificationRepository.Items);
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
            config.ProviderReference,
            "Completed");
        var signature = SignV1(payload);

        await service.ProcessWebhookAsync(payload, signature, null);
        var saveCount = unitOfWork.SaveChangesCount;
        var notification = Assert.Single(
            unitOfWork.NotificationRepository.Items);
        Assert.Equal(NotificationType.KycApproved, notification.Type);
        var updatedAt = unitOfWork.KycVerificationRepository.Items[0]
            .LastProviderUpdatedAt;
        await service.ProcessWebhookAsync(payload, signature, null);

        Assert.Equal(saveCount, unitOfWork.SaveChangesCount);
        Assert.Equal(
            updatedAt,
            unitOfWork.KycVerificationRepository.Items[0].LastProviderUpdatedAt);
        Assert.Single(unitOfWork.NotificationRepository.Items);
        var attempt = Assert.Single(
            unitOfWork.KycVerificationAttemptRepository.Items);
        Assert.Equal(KycProviderStatus.Approved, attempt.Status);
        Assert.Equal("Completed", attempt.RawStatus);
        Assert.Equal(dojahReference, attempt.ProviderReference);
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
            config.ProviderReference,
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
        var notification = Assert.Single(
            unitOfWork.NotificationRepository.Items);
        Assert.Equal(NotificationType.KycRejected, notification.Type);
        Assert.NotNull(kyc.LastProviderUpdatedAt);
    }

    [Fact]
    public async Task AbandonedDojahWebhookRejectsVerificationAndAllowsRetry()
    {
        const string dojahReference = "DJ-2A5B47E2B4";
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var abandonedPayload = Encoding.UTF8.GetBytes($$"""
        {
          "data": {
            "id": {
              "status": true
            },
            "selfie": {
              "status": false
            }
          },
          "status": false,
          "message": "Abandoned at Otp/Selfie step",
          "metadata": {
            "kyc_reference": "{{config.ProviderReference}}"
          },
          "reference_id": "{{dojahReference}}",
          "verification_mode": "LIVENESS",
          "verification_status": "Abandoned"
        }
        """);

        await service.ProcessWebhookAsync(
            abandonedPayload,
            SignV1(abandonedPayload),
            null);

        var kyc = Assert.Single(
            unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(KycStatus.Rejected, kyc.Status);
        Assert.Equal("Abandoned", kyc.ProviderStatus);
        Assert.Equal(
            "Abandoned at Otp/Selfie step",
            kyc.RejectionReason);
        Assert.Equal(config.ProviderReference, kyc.ProviderReference);
        Assert.Equal(dojahReference, kyc.DojahReference);
        Assert.NotEqual(kyc.ProviderReference, kyc.DojahReference);
        Assert.Null(kyc.VerifiedAt);
        Assert.NotNull(kyc.LastProviderUpdatedAt);

        var retry = await service.RetryAsync(kyc.UserId);

        Assert.Equal(KycStatus.Pending, retry.Status);
        Assert.StartsWith("PRYDE-", retry.ProviderReference);
        Assert.Equal(retry.ProviderReference, retry.ReferenceId);
        Assert.NotEqual(config.ProviderReference, retry.ProviderReference);
        Assert.DoesNotContain(config.ProviderReference, retry.ShareableLink);
        Assert.Null(kyc.DojahReference);
        Assert.Null(kyc.ProviderStatus);
        Assert.Null(kyc.RejectionReason);
        Assert.Null(kyc.LastProviderUpdatedAt);
    }

    [Fact]
    public async Task AbandonedWebhookWithoutMessageUsesSafeReason()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ProviderReference, "Abandoned");

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        var kyc = Assert.Single(
            unitOfWork.KycVerificationRepository.Items);
        Assert.Equal(KycStatus.Rejected, kyc.Status);
        Assert.Equal("Verification was abandoned.", kyc.RejectionReason);
    }

    [Fact]
    public async Task ValidV1SignatureIsAccepted()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ProviderReference, "Completed");

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        Assert.Equal(KycStatus.Approved, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    [Fact]
    public async Task ValidV2SignatureIsAccepted()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ProviderReference, "Completed");

        await service.ProcessWebhookAsync(payload, null, SignV2());

        Assert.Equal(KycStatus.Approved, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    [Fact]
    public async Task InvalidV1SignatureIsRejectedWithoutChangingStatus()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ProviderReference, "Completed");

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
        var payload = Payload(config.ProviderReference, "Completed");

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
        var payload = Payload(config.ProviderReference, "Completed");

        await service.ProcessWebhookAsync(payload, new string('0', 64), SignV2());

        Assert.Equal(KycStatus.Approved, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    [Fact]
    public async Task NeitherSignatureHeaderIsRejected()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ProviderReference, "Completed");

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
        var payload = Payload(config.ProviderReference, "Completed");

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
            $"{{\n  \"reference_id\": \"{config.ProviderReference}\",\n  \"verification_status\": \"Completed\"\n}}");

        await service.ProcessWebhookAsync(payload, SignV1(payload), null);

        Assert.Equal(KycStatus.Approved, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    [Fact]
    public async Task DuplicateWebhookIsIdempotent()
    {
        var unitOfWork = new TestUnitOfWork();
        var service = Service(unitOfWork);
        var config = await service.GetConfigAsync(Guid.NewGuid());
        var payload = Payload(config.ProviderReference, "Completed");
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
        var completed = Payload(config.ProviderReference, "Completed");
        await service.ProcessWebhookAsync(completed, SignV1(completed), null);
        var oldPending = Payload(config.ProviderReference, "Ongoing");

        await service.ProcessWebhookAsync(oldPending, SignV1(oldPending), null);

        Assert.Equal(KycStatus.Approved, unitOfWork.KycVerificationRepository.Items[0].Status);
    }

    private static DojahKycService Service(
        TestUnitOfWork unitOfWork,
        DojahSettings? settings = null) =>
        new(
            unitOfWork,
            Options.Create(settings ?? Settings()),
            NullLogger<DojahKycService>.Instance);

    private static TestUnitOfWork RejectedKycContext(
        out KycVerification kyc)
    {
        var unitOfWork = new TestUnitOfWork();
        kyc = new KycVerification
        {
            UserId = Guid.NewGuid(),
            Status = KycStatus.Rejected,
            ProviderName = "Dojah",
            ProviderReference = "PRYDE-old-rejected-attempt",
            DojahReference = "provider-generated-old-reference",
            ProviderStatus = "Failed",
            RejectionReason = "Identity check failed.",
            LastProviderUpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        return unitOfWork;
    }

    private static DojahSettings Settings() => new()
    {
        Enabled = true,
        BaseUrl = "https://api.dojah.io",
        AppId = "app-test",
        ApiToken = "api-token",
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
