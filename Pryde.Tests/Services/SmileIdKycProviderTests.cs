using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.DependencyInjection;
using Pryde.Services.Notifications.Interface;
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
            new KycProviderRequest(context.UserId, "VOTER_ID"));

        Assert.Equal("SmileId", result.Provider);
        Assert.Equal("HostedRedirect", result.IntegrationType);
        Assert.StartsWith("SMILE-GROUP-", result.Reference);
        Assert.Null(result.SessionUrl);
        var session = Assert.Single(result.Sessions);
        Assert.Equal("IdentityVerification", session.Flow);
        Assert.StartsWith("PRYDE-SMILE-", session.JobId);
        Assert.Equal("https://links.usesmileid.com/test", session.VerificationUrl);
        Assert.True(session.Required);
        Assert.Equal("Pending", session.Status);
        var request = Assert.Single(context.ApiClient.LinkRequests);
        Assert.Equal($"pryde-{context.UserId:N}", request.UserId);
        Assert.Equal(RoleNames.Passenger, request.Role);
        var requestedOption = Assert.Single(request.IdentityOptions);
        Assert.Equal("VOTER_ID", requestedOption.IdType);
        Assert.Equal("biometric_kyc", requestedOption.VerificationMethod);
        Assert.DoesNotContain("api-key", JsonSerializer.Serialize(result));
        var attempt = Assert.Single(
            context.UnitOfWork.KycVerificationAttemptRepository.Items);
        Assert.Equal("VOTER_ID", attempt.IdentityType);
        Assert.Equal("biometric_kyc", attempt.VerificationMethod);
        Assert.Equal("VOTER_ID:biometric_kyc", attempt.IdentityOptions);
        Assert.True(context.UnitOfWork.SaveChangesCount > 0);
        Assert.Equal(KycStatus.Pending, CurrentKyc(context).Status);
    }

    [Fact]
    public async Task PassengerLinkContainsOnlySelectedEnabledPassengerOption()
    {
        var context = Context(RoleNames.Passenger);

        await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "PASSPORT"));

        var options = Assert.Single(context.ApiClient.LinkRequests)
            .IdentityOptions;
        var option = Assert.Single(options);
        Assert.Equal("PASSPORT", option.IdType);
        Assert.Equal("doc_verification", option.VerificationMethod);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PassengerSessionRequiresSelectedIdentityType(
        string? selectedIdType)
    {
        var context = Context(RoleNames.Passenger);

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Provider.CreateSessionAsync(
                new KycProviderRequest(context.UserId, selectedIdType)));

        Assert.Empty(context.ApiClient.LinkRequests);
        Assert.Empty(context.UnitOfWork.KycVerificationAttemptRepository.Items);
    }

    [Theory]
    [InlineData("BVN")]
    [InlineData("UNKNOWN_ID")]
    public async Task PassengerSessionRejectsDisabledOrUnsupportedIdentityType(
        string selectedIdType)
    {
        var context = Context(RoleNames.Passenger);

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Provider.CreateSessionAsync(
                new KycProviderRequest(context.UserId, selectedIdType)));

        Assert.Empty(context.ApiClient.LinkRequests);
        Assert.Empty(context.UnitOfWork.KycVerificationAttemptRepository.Items);
    }

    [Fact]
    public async Task ExternalLinkCallRunsAfterPendingAttemptTransactionCommits()
    {
        var context = Context(RoleNames.Passenger);
        context.ApiClient.BeforeCreateLink = () =>
        {
            Assert.False(context.UnitOfWork.IsTransactionActive);
            var pending = Assert.Single(context.UnitOfWork.KycVerificationAttemptRepository.Items);
            Assert.Equal("CreatingLink", pending.RawStatus);
            Assert.Null(pending.VerificationUrl);
            Assert.Equal("VOTER_ID", pending.IdentityType);
            Assert.Equal("biometric_kyc", pending.VerificationMethod);
            Assert.Equal("VOTER_ID:biometric_kyc", pending.IdentityOptions);
        };

        await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "VOTER_ID"));

        Assert.Equal(2, context.UnitOfWork.TransactionCount);
        Assert.False(context.UnitOfWork.IsTransactionActive);
    }

    [Fact]
    public async Task RepeatedSessionRequestReturnsExistingLinkWithoutSecondHttpCall()
    {
        var context = Context(RoleNames.Passenger);
        var publicService = new KycProviderService(
            new KycProviderResolver(
                [new StubProvider("Dojah"), context.Provider],
                Options.Create(new KycSettings
                {
                    ActiveProvider = SmileIdKycProvider.ProviderName
                })),
            context.UnitOfWork,
            NullLogger<KycProviderService>.Instance);
        var firstResult = await publicService.CreateSessionAsync(
            context.UserId,
            "VOTER_ID");
        var first = Assert.Single(firstResult.Sessions);
        context.ApiClient.JobStatusFailure = new ServiceUnavailableException(
            "Transient job-status failure.");

        var secondResult = await publicService.CreateSessionAsync(
            context.UserId,
            "VOTER_ID");
        var second = Assert.Single(secondResult.Sessions);

        Assert.Equal("SmileId", secondResult.Provider);
        Assert.Equal("HostedRedirect", secondResult.IntegrationType);
        Assert.Equal(firstResult.Reference, secondResult.Reference);
        Assert.Equal(first.JobId, second.JobId);
        Assert.Equal(first.VerificationUrl, second.VerificationUrl);
        Assert.Equal("Pending", second.Status);
        Assert.Single(context.ApiClient.LinkRequests);
        Assert.Empty(context.ApiClient.JobStatusRequests);
        Assert.Single(
            context.UnitOfWork.KycVerificationAttemptRepository.Items);
    }

    [Theory]
    [InlineData("https://links.usesmileid.com/expired", -91)]
    [InlineData("not-a-hosted-url", 0)]
    public async Task ExpiredOrUnusableStoredLinkBecomesSafelyRetryable(
        string verificationUrl,
        int startedDaysFromNow)
    {
        var context = Context(RoleNames.Passenger);
        await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "VOTER_ID"));
        var attempt = Assert.Single(
            context.UnitOfWork.KycVerificationAttemptRepository.Items);
        attempt.VerificationUrl = verificationUrl;
        attempt.StartedAt = Now.UtcDateTime.AddDays(startedDaysFromNow);
        context.ApiClient.JobStatus = new SmileIdJobStatusResponse
        {
            Code = "2304"
        };

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Provider.CreateSessionAsync(
                new KycProviderRequest(context.UserId, "VOTER_ID")));

        Assert.Single(context.ApiClient.JobStatusRequests);
        Assert.Equal(KycProviderStatus.Rejected, attempt.Status);
        Assert.Equal("HostedLinkUnavailable", attempt.ResultCode);
        Assert.Equal(KycStatus.Rejected, CurrentKyc(context).Status);
        Assert.Single(context.ApiClient.LinkRequests);
        Assert.False(context.UnitOfWork.IsTransactionActive);
    }

    [Fact]
    public async Task MissingStoredLinkIsRejectedWithoutProviderRequest()
    {
        var context = Context(RoleNames.Passenger);
        await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "VOTER_ID"));
        var attempt = Assert.Single(
            context.UnitOfWork.KycVerificationAttemptRepository.Items);
        attempt.VerificationUrl = null;
        var publicService = new KycProviderService(
            new KycProviderResolver(
                [new StubProvider("Dojah"), context.Provider],
                Options.Create(new KycSettings
                {
                    ActiveProvider = SmileIdKycProvider.ProviderName
                })),
            context.UnitOfWork,
            NullLogger<KycProviderService>.Instance);

        var result = await publicService.CreateSessionAsync(
            context.UserId,
            "VOTER_ID");

        Assert.Equal(KycProviderStatus.Rejected, result.Status);
        Assert.Equal(KycProviderStatus.Rejected, attempt.Status);
        Assert.Equal(KycStatus.Rejected, CurrentKyc(context).Status);
        Assert.Single(context.ApiClient.LinkRequests);
        Assert.Empty(context.ApiClient.JobStatusRequests);
        Assert.True((await new KycService(context.UnitOfWork)
            .GetMineAsync(context.UserId)).CanRetry);
    }

    [Fact]
    public async Task FailedLinkCreationLeavesRetryableAttemptAndClosedTransaction()
    {
        var context = Context(RoleNames.Passenger);
        context.ApiClient.LinkFailure = new ServiceUnavailableException("Provider rejected request.");

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            context.Provider.CreateSessionAsync(
                new KycProviderRequest(context.UserId, "VOTER_ID")));

        var failed = Assert.Single(context.UnitOfWork.KycVerificationAttemptRepository.Items);
        Assert.Equal(KycProviderStatus.Rejected, failed.Status);
        Assert.Equal("LinkCreationFailed", failed.RawStatus);
        Assert.Equal(KycStatus.Rejected, CurrentKyc(context).Status);
        Assert.False(context.UnitOfWork.IsTransactionActive);

        context.ApiClient.LinkFailure = null;
        var retried = Assert.Single((await context.Provider.RetryAsync(
            new KycProviderRequest(context.UserId, "VOTER_ID"))).Sessions);
        Assert.NotEqual(failed.CorrelationReference, retried.JobId);
        Assert.Equal(2, context.UnitOfWork.KycVerificationAttemptRepository.Items.Count);
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
        Assert.Equal(SmileIdKycProvider.DriverLicenseFlow, licence.Flow);
        Assert.True(licence.Required);
        Assert.Equal(KycProviderStatus.Pending, licence.Status);
        Assert.False(licence.CallbackConfirmed);
    }

    [Fact]
    public async Task LegacyIncompleteAttemptDoesNotSendInvalidJobStatusRequest()
    {
        var context = Context(RoleNames.Passenger);
        var kyc = new KycVerification
        {
            UserId = context.UserId,
            ProviderName = SmileIdKycProvider.ProviderName,
            ProviderReference = "SMILE-GROUP-legacy",
            Status = KycStatus.Pending
        };
        context.UnitOfWork.KycVerificationRepository.Items.Add(kyc);
        context.UnitOfWork.KycVerificationAttemptRepository.Items.Add(
            new KycVerificationAttempt
            {
                KycVerificationId = kyc.Id,
                ProviderName = SmileIdKycProvider.ProviderName,
                CorrelationReference = "PRYDE-SMILE-legacy",
                AttemptGroupReference = kyc.ProviderReference,
                FlowType = SmileIdKycProvider.IdentityFlow,
                RawStatus = "CreatingLink"
            });

        var result = await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "VOTER_ID"));

        Assert.Empty(context.ApiClient.JobStatusRequests);
        Assert.Equal("SMILE-GROUP-legacy", result.Reference);
        Assert.Null(Assert.Single(result.Sessions).VerificationUrl);
    }

    [Fact]
    public async Task RecoveredMissingIdTypeDoesNotGuessBetweenLegacyOptions()
    {
        var context = Context(RoleNames.Passenger);
        var kycId = Guid.Parse(
            "05002d54-40bc-42d0-9f77-a2cd0ab99be6");
        const string oldGroupReference =
            "SMILE-GROUP-64a4219a6fd44e0ab2ecd041357c2999";
        const string oldCorrelationReference =
            "PRYDE-SMILE-4d015063f8bd44659d3708850f287c4b";
        const string oldVerificationUrl =
            "https://links.usesmileid.com/legacy-link";
        var kyc = new KycVerification
        {
            Id = kycId,
            UserId = context.UserId,
            ProviderName = SmileIdKycProvider.ProviderName,
            ProviderReference = oldGroupReference,
            ProviderStatus = "LinkCreated",
            Status = KycStatus.Pending
        };
        var legacyAttempt = new KycVerificationAttempt
        {
            Id = Guid.Parse("2d977f1d-df41-4cf1-bdc6-525065816ed4"),
            KycVerificationId = kyc.Id,
            ProviderName = SmileIdKycProvider.ProviderName,
            CorrelationReference = oldCorrelationReference,
            AttemptGroupReference = oldGroupReference,
            ExternalUserReference = $"pryde-{context.UserId:N}",
            FlowType = "BiometricKyc",
            Status = KycProviderStatus.Pending,
            RawStatus = "LinkCreated",
            ResultCode = null,
            VerificationUrl = oldVerificationUrl,
            IdentityOptions =
                "VOTER_ID:biometric_kyc,PASSPORT:doc_verification",
            IdentityType = null,
            VerificationMethod = null,
            ProviderEventTimestamp = null
        };
        context.UnitOfWork.KycVerificationRepository.Items.Add(kyc);
        var historicalDojahAttempt = new KycVerificationAttempt
        {
            KycVerificationId = kyc.Id,
            ProviderName = "Dojah",
            CorrelationReference = oldGroupReference,
            AttemptGroupReference = null,
            FlowType = null,
            Status = KycProviderStatus.Pending,
            RawStatus = "LinkCreated",
            ResultCode = "LinkCreated",
            VerificationUrl = null,
            IdentityOptions = null,
            IdentityType = null,
            VerificationMethod = null
        };
        context.UnitOfWork.KycVerificationAttemptRepository.Items.Add(
            historicalDojahAttempt);
        context.UnitOfWork.KycVerificationAttemptRepository.Items.Add(
            legacyAttempt);
        var resolver = new KycProviderResolver(
            [new StubProvider("Dojah"), context.Provider],
            Options.Create(new KycSettings
            {
                ActiveProvider = SmileIdKycProvider.ProviderName
            }));
        var publicService = new KycProviderService(
            resolver,
            context.UnitOfWork,
            NullLogger<KycProviderService>.Instance);
        context.ApiClient.JobStatus = new SmileIdJobStatusResponse
        {
            Code = "2302",
            Result = new SmileIdResultPayload
            {
                ResultCode = "0810",
                ResultText = "Machine pass",
                SmileJobId = "smile-internal",
                Country = "NG",
                IdType = null,
                IdTypeSnakeCase = "",
                PartnerParams = new SmileIdPartnerParams
                {
                    JobId = oldCorrelationReference,
                    UserId = legacyAttempt.ExternalUserReference,
                    Flow = "BiometricKyc",
                    Role = RoleNames.Passenger
                }
            }
        };

        var exception = await Record.ExceptionAsync(() =>
            publicService.CreateSessionAsync(context.UserId, "VOTER_ID"));

        Assert.IsType<ValidationException>(exception);
        Assert.Single(context.ApiClient.JobStatusRequests);
        Assert.False(context.UnitOfWork.IsTransactionActive);
        Assert.Equal(KycStatus.Pending, kyc.Status);
        Assert.Equal(KycProviderStatus.Pending, legacyAttempt.Status);
        Assert.Equal("LinkCreated", legacyAttempt.RawStatus);
        Assert.Null(legacyAttempt.ResultCode);
        Assert.Equal(2, context.UnitOfWork.KycVerificationAttemptRepository.Items.Count);
        Assert.Contains(
            legacyAttempt,
            context.UnitOfWork.KycVerificationAttemptRepository.Items);
        Assert.Contains(
            historicalDojahAttempt,
            context.UnitOfWork.KycVerificationAttemptRepository.Items);
        Assert.Equal(
            KycProviderStatus.Pending,
            historicalDojahAttempt.Status);
        Assert.Equal(oldGroupReference, legacyAttempt.AttemptGroupReference);
        Assert.Equal(oldVerificationUrl, legacyAttempt.VerificationUrl);
    }

    [Fact]
    public async Task ConcurrentPublicRetriesCreateOnlyOneReplacementAttempt()
    {
        var context = Context(RoleNames.Passenger);
        const string oldGroupReference = "SMILE-GROUP-concurrent-legacy";
        var kyc = new KycVerification
        {
            UserId = context.UserId,
            ProviderName = SmileIdKycProvider.ProviderName,
            ProviderReference = oldGroupReference,
            ProviderStatus = "LegacyIdentityDataMissing",
            Status = KycStatus.Rejected
        };
        context.UnitOfWork.KycVerificationRepository.Items.Add(kyc);
        context.UnitOfWork.KycVerificationAttemptRepository.Items.Add(
            new KycVerificationAttempt
            {
                KycVerificationId = kyc.Id,
                ProviderName = SmileIdKycProvider.ProviderName,
                CorrelationReference = "PRYDE-SMILE-concurrent-legacy",
                AttemptGroupReference = oldGroupReference,
                ExternalUserReference = $"pryde-{context.UserId:N}",
                FlowType = SmileIdKycProvider.LegacyBiometricFlow,
                Status = KycProviderStatus.Rejected,
                RawStatus = "LinkCreated",
                ResultCode = "LegacyIdentityDataMissing",
                VerificationUrl = "https://links.usesmileid.com/legacy",
                IdentityOptions =
                    "VOTER_ID:biometric_kyc,PASSPORT:doc_verification",
                IdentityType = "VOTER_ID",
                VerificationMethod = "biometric_kyc"
            });
        var publicService = new KycProviderService(
            new KycProviderResolver(
                [new StubProvider("Dojah"), context.Provider],
                Options.Create(new KycSettings
                {
                    ActiveProvider = SmileIdKycProvider.ProviderName
                })),
            context.UnitOfWork,
            NullLogger<KycProviderService>.Instance);

        async Task<(KycProviderResult? Result, Exception? Exception)> RetryAsync()
        {
            try
            {
                return (await publicService.RetryAsync(context.UserId), null);
            }
            catch (Exception exception)
            {
                return (null, exception);
            }
        }

        var outcomes = await Task.WhenAll(RetryAsync(), RetryAsync());

        var succeeded = Assert.Single(
            outcomes,
            outcome => outcome.Result is not null);
        var rejected = Assert.Single(
            outcomes,
            outcome => outcome.Exception is not null);
        Assert.IsType<ConflictException>(rejected.Exception);
        Assert.StartsWith("SMILE-GROUP-", succeeded.Result!.Reference);
        Assert.NotEqual(oldGroupReference, succeeded.Result.Reference);
        Assert.NotNull(Assert.Single(succeeded.Result.Sessions).VerificationUrl);
        Assert.Equal(
            2,
            context.UnitOfWork.KycVerificationAttemptRepository.Items.Count);
        Assert.Single(context.ApiClient.LinkRequests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("\"\"")]
    public async Task EmptyLegacyIdentityOptionRepresentationsRejectBeforeStatusRequest(
        string? identityOptions)
    {
        var context = Context(RoleNames.Passenger);
        var groupReference = $"SMILE-GROUP-{Guid.NewGuid():N}";
        var kyc = new KycVerification
        {
            UserId = context.UserId,
            ProviderName = SmileIdKycProvider.ProviderName,
            ProviderReference = groupReference,
            ProviderStatus = "LinkCreated",
            Status = KycStatus.Pending
        };
        context.UnitOfWork.KycVerificationRepository.Items.Add(kyc);
        context.UnitOfWork.KycVerificationAttemptRepository.Items.Add(
            new KycVerificationAttempt
            {
                KycVerificationId = kyc.Id,
                ProviderName = SmileIdKycProvider.ProviderName,
                CorrelationReference = $"PRYDE-SMILE-{Guid.NewGuid():N}",
                AttemptGroupReference = groupReference,
                ExternalUserReference = $"pryde-{context.UserId:N}",
                FlowType = "BiometricKyc",
                Status = KycProviderStatus.Pending,
                RawStatus = "LinkCreated",
                VerificationUrl = "https://links.usesmileid.com/legacy",
                IdentityOptions = identityOptions,
                IdentityType = "",
                VerificationMethod = " "
            });
        var publicService = new KycProviderService(
            new KycProviderResolver(
                [new StubProvider("Dojah"), context.Provider],
                Options.Create(new KycSettings
                {
                    ActiveProvider = SmileIdKycProvider.ProviderName
                })),
            context.UnitOfWork,
            NullLogger<KycProviderService>.Instance);

        var result = await publicService.CreateSessionAsync(
            context.UserId,
            "VOTER_ID");

        Assert.Equal(KycProviderStatus.Rejected, result.Status);
        Assert.Empty(context.ApiClient.JobStatusRequests);
        Assert.True((await new KycService(context.UnitOfWork)
            .GetMineAsync(context.UserId)).CanRetry);
    }

    [Fact]
    public async Task DriverSessionContainsOnlyDriversLicenceDocumentVerification()
    {
        var context = Context(RoleNames.Driver);

        var result = await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId));

        var licence = Assert.Single(result.Sessions);
        Assert.Equal("DriverLicenseVerification", licence.Flow);
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
            new KycProviderRequest(context.UserId, "VOTER_ID"))).Sessions);

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
    public async Task PassengerDocumentSelectionUsesAuthenticatedResultProductNotSessionFlow()
    {
        var context = Context(RoleNames.Passenger);
        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "PASSPORT"))).Sessions);
        Assert.Equal(SmileIdKycProvider.IdentityFlow, session.Flow);

        await context.Provider.ProcessCallbackAsync(Callback(
            session,
            "0810",
            "Document verified",
            idType: "PASSPORT"));

        var attempt = CurrentAttempt(context, session);
        Assert.Equal("PASSPORT", attempt.IdentityType);
        Assert.Equal("doc_verification", attempt.VerificationMethod);
        Assert.Equal(KycProviderStatus.Approved, attempt.Status);
        Assert.Equal(KycStatus.Approved, CurrentKyc(context).Status);
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
    public async Task ApprovedDriverCallbackPersistsAttemptBeforeParentRecalculation()
    {
        var context = Context(RoleNames.Driver);
        var licence = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId))).Sessions);
        var kyc = CurrentKyc(context);
        var attempt = CurrentAttempt(context, licence);
        kyc.Status = KycStatus.Submitted;
        kyc.VerifiedAt = null;
        kyc.ProviderStatus =
            $"{SmileIdKycProvider.DriverLicenseFlow}:LinkCreated";
        kyc.RejectionReason = "Previous rejection";
        attempt.Status = KycProviderStatus.Pending;
        attempt.RawStatus = "LinkCreated";
        var payload = Callback(
            licence,
            "0810",
            "Document Verified");
        var saveCount = context.UnitOfWork.SaveChangesCount;

        await context.Provider.ProcessCallbackAsync(payload);

        Assert.Equal(KycProviderStatus.Approved, attempt.Status);
        Assert.Equal(KycStatus.Approved, kyc.Status);
        Assert.Equal(Now.UtcDateTime, kyc.VerifiedAt);
        Assert.Null(kyc.RejectionReason);
        Assert.Equal(
            $"{SmileIdKycProvider.DriverLicenseFlow}:Document Verified",
            kyc.ProviderStatus);
        Assert.Equal(saveCount + 3, context.UnitOfWork.SaveChangesCount);

        await context.Provider.ProcessCallbackAsync(payload);

        Assert.Equal(saveCount + 3, context.UnitOfWork.SaveChangesCount);
        Assert.Equal(KycProviderStatus.Approved, attempt.Status);
        Assert.Equal(KycStatus.Approved, kyc.Status);
        Assert.Equal(Now.UtcDateTime, kyc.VerifiedAt);
        Assert.Null(kyc.RejectionReason);
        Assert.Equal(
            $"{SmileIdKycProvider.DriverLicenseFlow}:Document Verified",
            kyc.ProviderStatus);
        Assert.Single(
            context.Email.Messages,
            message => message.Subject ==
                "Your Pryde identity verification is approved");
    }

    [Fact]
    public async Task RejectedCallbackSendsReasonEmailOnceAndKeepsNotification()
    {
        var context = Context(RoleNames.Driver);
        var licence = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId))).Sessions);
        var payload = Callback(
            licence,
            "0811",
            "Driver licence could not be verified");

        await context.Provider.ProcessCallbackAsync(payload);
        await context.Provider.ProcessCallbackAsync(payload);

        Assert.Equal(KycStatus.Rejected, CurrentKyc(context).Status);
        var email = Assert.Single(context.Email.Messages);
        Assert.Equal(
            "Your Pryde identity verification was unsuccessful",
            email.Subject);
        Assert.Contains("Driver licence could not be verified", email.Body);
        Assert.Equal(
            NotificationType.KycRejected,
            Assert.Single(context.UnitOfWork.NotificationRepository.Items).Type);
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
            new KycProviderRequest(context.UserId, "VOTER_ID"))).Sessions);
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
            new KycProviderRequest(context.UserId, "VOTER_ID"))).Sessions);

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
            new KycProviderRequest(context.UserId, "VOTER_ID"))).Sessions);
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
            new KycProviderRequest(context.UserId, "VOTER_ID"))).Sessions);
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
    public async Task NinV2CallbackWithoutIdTypeUsesSingleStoredOptionAndApproves()
    {
        var context = Context(RoleNames.Passenger);
        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "VOTER_ID"))).Sessions);
        var attempt = CurrentAttempt(context, session);
        attempt.IdentityOptions = "NIN_V2:biometric_kyc";

        await context.Provider.ProcessCallbackAsync(Callback(
            session,
            "0810",
            "Machine comparison passed",
            omitIdType: true));
        await context.Provider.ProcessCallbackAsync(Callback(
            session,
            "1012",
            "ID authority returned a record",
            timestamp: Now.AddSeconds(1).ToString("O"),
            omitIdType: true));

        Assert.Equal("NIN_V2", attempt.IdentityType);
        Assert.Equal("biometric_kyc", attempt.VerificationMethod);
        Assert.Equal(KycProviderStatus.Approved, attempt.Status);
        Assert.Equal(KycStatus.Approved, CurrentKyc(context).Status);
    }

    [Fact]
    public async Task NinV2RejectedCallbackWithoutIdTypePersistsRejection()
    {
        var context = Context(RoleNames.Passenger);
        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "VOTER_ID"))).Sessions);
        var attempt = CurrentAttempt(context, session);
        attempt.IdentityOptions = "NIN_V2:biometric_kyc";

        await context.Provider.ProcessCallbackAsync(Callback(
            session,
            "0811",
            "Identity verification failed",
            omitIdType: true));

        Assert.Equal("NIN_V2", attempt.IdentityType);
        Assert.Equal("biometric_kyc", attempt.VerificationMethod);
        Assert.Equal(KycProviderStatus.Rejected, attempt.Status);
        Assert.Equal(KycStatus.Rejected, CurrentKyc(context).Status);
    }

    [Fact]
    public async Task SuppliedWrongIdTypeStillFailsValidation()
    {
        var context = Context(RoleNames.Passenger);
        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "VOTER_ID"))).Sessions);
        CurrentAttempt(context, session).IdentityOptions =
            "NIN_V2:biometric_kyc";

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Provider.ProcessCallbackAsync(Callback(
                session,
                "0810",
                "Machine comparison passed",
                idType: "VOTER_ID")));

        Assert.Equal(KycStatus.Pending, CurrentKyc(context).Status);
        Assert.Equal(
            KycProviderStatus.Pending,
            CurrentAttempt(context, session).Status);
    }

    [Fact]
    public async Task CallbackWithoutIdTypeAndMultipleStoredOptionsIsRejectedAsAmbiguous()
    {
        var context = Context(RoleNames.Passenger);
        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "VOTER_ID"))).Sessions);
        var attempt = CurrentAttempt(context, session);
        attempt.IdentityOptions =
            "VOTER_ID:biometric_kyc,PASSPORT:doc_verification";
        attempt.IdentityType = null;
        attempt.VerificationMethod = null;

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Provider.ProcessCallbackAsync(Callback(
                session,
                "0810",
                "Machine comparison passed",
                omitIdType: true)));

        Assert.Equal(KycStatus.Pending, CurrentKyc(context).Status);
        Assert.Equal(
            KycProviderStatus.Pending,
            CurrentAttempt(context, session).Status);
    }

    [Fact]
    public async Task LegacyNinV2AttemptWithoutOptionSnapshotRemainsCallbackCompatible()
    {
        var context = Context(RoleNames.Passenger);
        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "VOTER_ID"))).Sessions);
        CurrentAttempt(context, session).IdentityOptions = null;
        CurrentAttempt(context, session).FlowType = SmileIdKycProvider.LegacyBiometricFlow;

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
    public async Task LegacyStoredFlowIsReturnedUsingNeutralLabel()
    {
        var context = Context(RoleNames.Passenger);
        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "VOTER_ID"))).Sessions);
        CurrentAttempt(context, session).FlowType = SmileIdKycProvider.LegacyBiometricFlow;
        CurrentAttempt(context, session).IdentityType = "VOTER_ID";
        CurrentAttempt(context, session).VerificationMethod = "biometric_kyc";

        var providerResult = await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "VOTER_ID"));
        var mine = await new KycService(context.UnitOfWork).GetMineAsync(context.UserId);

        Assert.Equal(
            SmileIdKycProvider.IdentityFlow,
            Assert.Single(providerResult.Sessions).Flow);
        var mineFlow = Assert.Single(mine.Flows);
        Assert.Equal(SmileIdKycProvider.IdentityFlow, mineFlow.Flow);
        Assert.Equal(KycProviderStatus.Pending, mineFlow.Status);
        Assert.Equal("VOTER_ID", mineFlow.IdType);
        Assert.Equal("biometric_kyc", mineFlow.VerificationMethod);
    }

    [Fact]
    public async Task RetryCreatesNewJobsAndPreservesAttemptHistory()
    {
        var context = Context(RoleNames.Passenger);
        var first = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "VOTER_ID"))).Sessions);
        await context.Provider.ProcessCallbackAsync(Callback(first, "0811", "Face mismatch"));

        var second = Assert.Single((await context.Provider.RetryAsync(
            new KycProviderRequest(context.UserId))).Sessions);

        Assert.NotEqual(first.JobId, second.JobId);
        Assert.Equal(2, context.UnitOfWork.KycVerificationAttemptRepository.Items.Count);
        Assert.Equal(KycProviderStatus.Rejected, CurrentAttempt(context, first).Status);
        var retryAttempt = CurrentAttempt(context, second);
        Assert.Equal(KycProviderStatus.Pending, retryAttempt.Status);
        Assert.Equal("VOTER_ID", retryAttempt.IdentityType);
        Assert.Equal("biometric_kyc", retryAttempt.VerificationMethod);
        var retryOption = Assert.Single(
            context.ApiClient.LinkRequests.Last().IdentityOptions);
        Assert.Equal("VOTER_ID", retryOption.IdType);
    }

    [Fact]
    public async Task JobStatusHistoryRecoversSubmittedAttemptAfterMissedFinalCallback()
    {
        var context = Context(RoleNames.Passenger);
        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "VOTER_ID"))).Sessions);
        var attempt = CurrentAttempt(context, session);
        attempt.Status = KycProviderStatus.Submitted;
        attempt.RawStatus = "Submitted";
        attempt.ProviderEventTimestamp = Now.UtcDateTime;
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
            new KycProviderRequest(context.UserId, "VOTER_ID"));

        Assert.Equal(KycStatus.Approved, CurrentKyc(context).Status);
        Assert.Equal(KycProviderStatus.Approved, attempt.Status);
        Assert.Single(context.ApiClient.JobStatusRequests);
    }

    [Theory]
    [InlineData("Sandbox")]
    [InlineData("Production")]
    public async Task EnvironmentUsesHostedLinkFromConfiguredApiClient(
        string environment)
    {
        var context = Context(RoleNames.Passenger, environment);

        var session = Assert.Single((await context.Provider.CreateSessionAsync(
            new KycProviderRequest(context.UserId, "VOTER_ID"))).Sessions);

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
            ["SmileId:PassengerIdentityOptions:0:IdType"] = "NIN_V2",
            ["SmileId:PassengerIdentityOptions:0:VerificationMethod"] = "biometric_kyc",
            ["SmileId:DriverIdentityOptions:0:IdType"] = "DRIVERS_LICENSE",
            ["SmileId:DriverIdentityOptions:0:VerificationMethod"] = "doc_verification"
        };
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IUnitOfWork>(new TestUnitOfWork());
        builder.Services.AddSingleton<INotificationService>(serviceProvider =>
            new NotificationService(serviceProvider.GetRequiredService<IUnitOfWork>()));
        builder.Services.AddSingleton<IEmailService, CapturingEmailService>();
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
        unitOfWork.UserRepository.Items.Add(new User
        {
            Id = userId,
            Email = "smile-user@test.local",
            PhoneNumber = "08000000000"
        });
        unitOfWork.UserRoleRepository.Items.Add(new UserRole
        {
            UserId = userId,
            Role = new Role { Name = role }
        });
        var apiClient = new StubSmileIdApiClient();
        var email = new CapturingEmailService();
        var settings = Settings(environment);
        var provider = new SmileIdKycProvider(
            unitOfWork,
            apiClient,
            Options.Create(settings),
            NullLogger<SmileIdKycProvider>.Instance,
            new NotificationService(unitOfWork),
            email,
            new FixedTimeProvider(Now));
        return new TestContext(
            unitOfWork,
            provider,
            apiClient,
            email,
            userId);
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
            new() { IdType = "NIN_V2", VerificationMethod = "biometric_kyc", Enabled = false },
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
        bool legacyPartnerParams = false,
        bool omitIdType = false)
    {
        var partnerParams = new Dictionary<string, object?>
        {
            ["job_id"] = session.JobId,
            ["user_id"] = userId ?? CallbackUsers[session.JobId]
        };
        if (legacyPartnerParams)
        {
            partnerParams["job_type"] = session.Flow == SmileIdKycProvider.DriverLicenseFlow ? "6" : "1";
        }
        else
        {
            partnerParams["flow"] = session.Flow;
            partnerParams["role"] = role ?? (session.Flow == SmileIdKycProvider.DriverLicenseFlow
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
            [pascalCase ? "PartnerParams" : "partner_params"] = partnerParams
        };
        if (!omitIdType)
        {
            payload[pascalCase ? "IDType" : "id_type"] = idType ??
                (session.Flow == SmileIdKycProvider.IdentityFlow
                    ? "VOTER_ID"
                    : "DRIVERS_LICENSE");
        }
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
            IdType = session.Flow == SmileIdKycProvider.IdentityFlow ? "VOTER_ID" : "DRIVERS_LICENSE",
            PartnerParams = new SmileIdPartnerParams
            {
                JobId = session.JobId,
                UserId = CallbackUsers[session.JobId],
                Flow = session.Flow,
                Role = session.Flow == SmileIdKycProvider.DriverLicenseFlow
                    ? RoleNames.Driver
                    : RoleNames.Passenger
            }
        };

    private sealed record TestContext(
        TestUnitOfWork UnitOfWork,
        SmileIdKycProvider Provider,
        StubSmileIdApiClient ApiClient,
        CapturingEmailService Email,
        Guid UserId);

    private sealed class CapturingEmailService : IEmailService
    {
        public List<(string ToEmail, string Subject, string Body)> Messages { get; } = [];

        public Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
        {
            Messages.Add((toEmail, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubSmileIdApiClient : ISmileIdApiClient
    {
        public List<SmileIdLinkRequest> LinkRequests { get; } = [];
        public List<(string UserId, string JobId)> JobStatusRequests { get; } = [];
        public Action? BeforeCreateLink { get; set; }
        public Exception? LinkFailure { get; set; }
        public Exception? JobStatusFailure { get; set; }
        public SmileIdJobStatusResponse JobStatus { get; set; } = new()
        {
            Code = "2304"
        };

        public Task<SmileIdLinkResponse> CreateSingleUseLinkAsync(
            SmileIdLinkRequest request,
            CancellationToken cancellationToken = default)
        {
            BeforeCreateLink?.Invoke();
            LinkRequests.Add(request);
            if (LinkFailure is not null)
            {
                return Task.FromException<SmileIdLinkResponse>(LinkFailure);
            }
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
            CancellationToken cancellationToken = default)
        {
            JobStatusRequests.Add((userId, jobId));
            if (JobStatusFailure is not null)
            {
                return Task.FromException<SmileIdJobStatusResponse>(
                    JobStatusFailure);
            }
            return Task.FromResult(JobStatus);
        }

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
            NullLogger<SmileIdApiClient>.Instance,
            new TestHostEnvironment { EnvironmentName = Environments.Development },
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
            NullLogger<SmileIdApiClient>.Instance,
            new TestHostEnvironment { EnvironmentName = Environments.Development },
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero)));

        var result = await api.CreateSingleUseLinkAsync(new SmileIdLinkRequest(
            "Pryde identity job-1",
            "pryde-user",
            "job-1",
            RoleNames.Passenger,
            SmileIdKycProvider.IdentityFlow,
            [
                new("NG", "VOTER_ID", "biometric_kyc"),
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
        Assert.Equal("VOTER_ID", idTypes[0].GetProperty("id_type").GetString());
        Assert.Equal("biometric_kyc", idTypes[0].GetProperty("verification_method").GetString());
        Assert.Equal("PASSPORT", idTypes[1].GetProperty("id_type").GetString());
        Assert.Equal("doc_verification", idTypes[1].GetProperty("verification_method").GetString());
    }

    [Theory]
    [InlineData("{\"code\":\"2213\",\"error\":\"A required parameter is missing\",\"field\":\"callback_url\"}", "[2213] A required parameter is missing")]
    [InlineData("Bad Request", "[400] Bad Request")]
    [InlineData("", "[400] Provider returned an empty error response.")]
    [InlineData("{malformed", "[400] Provider returned an unreadable error response.")]
    public async Task LinkErrorsReturnSanitizedDevelopmentMessage(
        string body,
        string expected)
    {
        var logger = new CapturingLogger<SmileIdApiClient>();
        var api = ErrorApi(body, Environments.Development, logger);

        var exception = await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            api.CreateSingleUseLinkAsync(LinkRequest()));

        Assert.Equal($"Smile ID rejected link creation: {expected}", exception.Message);
        Assert.DoesNotContain("api-key", string.Join(' ', logger.Messages));
        Assert.DoesNotContain("signature", string.Join(' ', logger.Messages), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProductionLinkErrorIsGenericAndLogIsRedacted()
    {
        var logger = new CapturingLogger<SmileIdApiClient>();
        var api = ErrorApi(
            "{\"code\":\"2213\",\"error\":\"Invalid https://secret.example/u user@example.com\",\"field\":\"redirect_url\"}",
            Environments.Production,
            logger);

        var exception = await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            api.CreateSingleUseLinkAsync(LinkRequest()));

        Assert.Equal("Smile ID link creation is temporarily unavailable.", exception.Message);
        var logs = string.Join(' ', logger.Messages);
        Assert.Contains("2213", logs);
        Assert.Contains("redirect_url", logs);
        Assert.DoesNotContain("secret.example", logs);
        Assert.DoesNotContain("user@example.com", logs);
    }

    [Fact]
    public async Task AccountCapabilityErrorIdentifiesIdTypesWithoutLoggingRequest()
    {
        var logger = new CapturingLogger<SmileIdApiClient>();
        var api = ErrorApi(
            "{\"code\":\"2413\",\"error\":\"NIN_SLIP is not enabled on your account for the country NG and verification method biometric_kyc\"}",
            Environments.Development,
            logger);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            api.CreateSingleUseLinkAsync(LinkRequest()));

        var logs = string.Join(' ', logger.Messages);
        Assert.Contains("2413", logs);
        Assert.Contains("Field: id_types", logs);
        Assert.DoesNotContain("partner-test", logs);
        Assert.DoesNotContain("pryde-user", logs);
    }

    private static SmileIdApiClient ErrorApi(
        string body,
        string environment,
        ILogger<SmileIdApiClient> logger)
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        });
        return new SmileIdApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://testapi.smileidentity.com/") },
            Options.Create(new SmileIdSettings
            {
                PartnerId = "partner-test",
                ApiKey = "api-key",
                CompanyName = "Pryde",
                CallbackUrl = "https://api.example.test/callback",
                RedirectUrl = "https://app.example.test/onboarding/kyc",
                DataPrivacyPolicyUrl = "https://example.test/privacy"
            }),
            logger,
            new TestHostEnvironment { EnvironmentName = environment },
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero)));
    }

    private static SmileIdLinkRequest LinkRequest() => new(
        "Pryde identity job-1",
        "pryde-user",
        "job-1",
        RoleNames.Passenger,
        SmileIdKycProvider.IdentityFlow,
        [new("NG", "VOTER_ID", "biometric_kyc")]);

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

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
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
