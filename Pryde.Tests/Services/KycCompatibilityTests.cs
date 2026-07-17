using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Storage.Enums;
using Pryde.Services.Storage.Interface;
using Pryde.Services.Storage.Models;
using Pryde.Tests.TestInfrastructure;
using Pryde.Contracts.RequestModels;
using Microsoft.AspNetCore.Http;

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
