using System.Security.Cryptography;
using System.Text;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Settings;
using Pryde.Services.Storage.Enums;
using Pryde.Services.Storage.Interface;
using Pryde.Services.Storage.Models;
using Pryde.Tests.TestInfrastructure;
using Pryde.Contracts.RequestModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pryde.Domain.Common.Exceptions;

namespace Pryde.Tests.Services;

public class KycCompatibilityTests
{
    [Fact]
    public async Task ExistingManualDocumentUploadStillWorks()
    {
        var unitOfWork = new TestUnitOfWork();
        var userId = Guid.NewGuid();
        await using var content = new MemoryStream([1, 2, 3]);
        var file = new FormFile(content, 0, content.Length, "file", "selfie.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var result = await new KycService(unitOfWork, new FakeFileStorageService())
            .UploadDocumentsAsync(userId, new KycDocumentUploadRequest
            {
                BiometricVerification = file
            });

        Assert.Equal(KycStatus.Pending, result.Status);
        Assert.Equal("https://files.test/selfie.jpg", result.BiometricVerificationUrl);
    }

    [Fact]
    public async Task ExistingAdminApprovalStillWorks()
    {
        var unitOfWork = Context(out var kyc);

        var result = await new KycService(unitOfWork, null!).ApproveAsync(kyc.UserId);

        Assert.Equal(KycStatus.Approved, result.Status);
        Assert.NotNull(result.VerifiedAt);
    }

    [Fact]
    public async Task ExistingAdminRejectionStillWorksAndStoresSanitizedReason()
    {
        var unitOfWork = Context(out var kyc);

        var result = await new KycService(unitOfWork, null!).RejectAsync(
            kyc.UserId,
            "  document   mismatch  ");

        Assert.Equal(KycStatus.Rejected, result.Status);
        Assert.Equal("document mismatch", result.RejectionReason);
    }

    [Fact]
    public async Task ExistingMineStatusReturnsOnlyTheAuthenticatedUsersRecord()
    {
        var unitOfWork = Context(out var mine);
        unitOfWork.KycVerificationRepository.Items.Add(new KycVerification
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = KycStatus.Rejected
        });

        var result = await new KycService(unitOfWork, null!).GetMineAsync(mine.UserId);

        Assert.Equal(mine.Id, result.Id);
        Assert.Equal(mine.UserId, result.UserId);
    }

    [Fact]
    public async Task FailedDojahKycFollowedByDocumentReuploadStartsFreshPendingReview()
    {
        var unitOfWork = Context(out var kyc);
        kyc.Status = KycStatus.Rejected;
        kyc.ProviderName = "Dojah";
        kyc.ProviderReference = "PRYDE-old-failed-attempt";
        kyc.ProviderStatus = "Failed";
        kyc.RejectionReason = "Face mismatch";
        kyc.LastProviderUpdatedAt = DateTime.UtcNow.AddMinutes(-5);

        var result = await UploadBiometricAsync(unitOfWork, kyc.UserId);

        Assert.Equal(KycStatus.Pending, result.Status);
        Assert.Equal("Dojah", result.ProviderName);
        Assert.Null(result.ProviderReference);
        Assert.Null(result.ProviderStatus);
        Assert.Null(result.RejectionReason);
        Assert.Null(result.VerifiedAt);
        Assert.Null(result.LastProviderUpdatedAt);
    }

    [Fact]
    public async Task RejectedManualKycFollowedByDocumentReuploadStartsFreshPendingReview()
    {
        var unitOfWork = Context(out var kyc);
        kyc.Status = KycStatus.Rejected;
        kyc.RejectionReason = "Document mismatch";

        var result = await UploadBiometricAsync(unitOfWork, kyc.UserId);

        Assert.Equal(KycStatus.Pending, result.Status);
        Assert.Null(result.RejectionReason);
        Assert.Null(result.VerifiedAt);
        Assert.Null(result.ProviderStatus);
        Assert.Null(result.LastProviderUpdatedAt);
    }

    [Fact]
    public async Task ApprovedKycDocumentReplacementDoesNotReopenFinalizedReview()
    {
        var unitOfWork = Context(out var kyc);
        var verifiedAt = DateTime.UtcNow.AddDays(-1);
        var providerUpdatedAt = verifiedAt.AddMinutes(-1);
        kyc.Status = KycStatus.Approved;
        kyc.VerifiedAt = verifiedAt;
        kyc.ProviderName = "Dojah";
        kyc.ProviderReference = "PRYDE-approved-attempt";
        kyc.ProviderStatus = "Completed";
        kyc.LastProviderUpdatedAt = providerUpdatedAt;

        var result = await UploadBiometricAsync(unitOfWork, kyc.UserId);

        Assert.Equal(KycStatus.Approved, result.Status);
        Assert.Equal(verifiedAt, result.VerifiedAt);
        Assert.Equal("Dojah", result.ProviderName);
        Assert.Equal("PRYDE-approved-attempt", result.ProviderReference);
        Assert.Equal("Completed", result.ProviderStatus);
        Assert.Equal(providerUpdatedAt, result.LastProviderUpdatedAt);
    }

    [Fact]
    public async Task PendingKycDocumentUploadInvalidatesPreviousProviderAttempt()
    {
        var unitOfWork = Context(out var kyc);
        kyc.ProviderName = "Dojah";
        kyc.ProviderReference = "PRYDE-in-progress-attempt";
        kyc.ProviderStatus = "Ongoing";
        kyc.LastProviderUpdatedAt = DateTime.UtcNow.AddMinutes(-1);

        var result = await UploadBiometricAsync(unitOfWork, kyc.UserId);

        Assert.Equal(KycStatus.Pending, result.Status);
        Assert.Equal("Dojah", result.ProviderName);
        Assert.Null(result.ProviderReference);
        Assert.Null(result.ProviderStatus);
        Assert.Null(result.LastProviderUpdatedAt);
    }

    [Fact]
    public async Task OldWebhookAfterDocumentRetryCannotOverwriteFreshPendingReview()
    {
        const string oldReference = "PRYDE-old-retry-attempt";
        var unitOfWork = Context(out var kyc);
        kyc.Status = KycStatus.Rejected;
        kyc.ProviderName = "Dojah";
        kyc.ProviderReference = oldReference;
        kyc.ProviderStatus = "Failed";
        kyc.RejectionReason = "Provider check failed";
        kyc.LastProviderUpdatedAt = DateTime.UtcNow.AddMinutes(-5);

        await UploadBiometricAsync(unitOfWork, kyc.UserId);

        var dojahService = DojahService(unitOfWork);
        var newConfig = await dojahService.GetConfigAsync(kyc.UserId);
        Assert.NotEqual(oldReference, newConfig.ReferenceId);

        var oldPayload = Encoding.UTF8.GetBytes(
            $"{{\"reference_id\":\"{oldReference}\",\"verification_status\":\"Completed\"}}");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            dojahService.ProcessWebhookAsync(oldPayload, Sign(oldPayload), null));

        Assert.Equal(KycStatus.Pending, kyc.Status);
        Assert.Equal(newConfig.ReferenceId, kyc.ProviderReference);
        Assert.Null(kyc.ProviderStatus);
        Assert.Null(kyc.LastProviderUpdatedAt);
    }

    private static TestUnitOfWork Context(out KycVerification kyc)
    {
        var unitOfWork = new TestUnitOfWork();
        kyc = new KycVerification
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = KycStatus.Pending
        };
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        return unitOfWork;
    }

    private static async Task<Pryde.Contracts.ResponseModels.KycVerificationResponseDto> UploadBiometricAsync(
        TestUnitOfWork unitOfWork,
        Guid userId)
    {
        var content = new MemoryStream([1, 2, 3]);
        var file = new FormFile(content, 0, content.Length, "file", "replacement.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        return await new KycService(unitOfWork, new FakeFileStorageService())
            .UploadDocumentsAsync(userId, new KycDocumentUploadRequest
            {
                BiometricVerification = file
            });
    }

    private static DojahKycService DojahService(TestUnitOfWork unitOfWork) =>
        new(unitOfWork, Options.Create(DojahSettings()), NullLogger<DojahKycService>.Instance);

    private static DojahSettings DojahSettings() => new()
    {
        Enabled = true,
        AppId = "app-test",
        PublicKey = "public-test",
        PrivateKey = "private-test",
        ShareableLink = "https://identity.dojah.io/?widget_id=widget-test"
    };

    private static string Sign(byte[] payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(DojahSettings().PrivateKey));
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }

    private sealed class FakeFileStorageService : IFileStorageService
    {
        public Task<FileUploadResult> UploadAsync(Stream fileStream, string fileName, string contentType, FileCategory category, Guid ownerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FileUploadResult("kyc/selfie.jpg", "https://files.test/selfie.jpg"));

        public Task<string> GetReadUrlAsync(string fileKey, FileCategory category, CancellationToken cancellationToken = default) =>
            Task.FromResult("https://files.test/selfie.jpg");

        public Task DeleteAsync(string fileKey, FileCategory category, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
