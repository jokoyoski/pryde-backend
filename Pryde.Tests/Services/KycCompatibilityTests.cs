using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class KycCompatibilityTests
{
    [Fact]
    public async Task ExistingAdminApprovalStillWorks()
    {
        var unitOfWork = Context(out var kyc);
        kyc.BiometricVerificationUrl = "https://files.test/selfie.jpg";
        kyc.Status = KycStatus.Submitted;

        var result = await new KycService(unitOfWork)
            .ApproveAsync(kyc.UserId);

        Assert.Equal(KycStatus.Approved, result.Status);
        Assert.NotNull(result.VerifiedAt);
        var notification = Assert.Single(
            unitOfWork.NotificationRepository.Items);
        Assert.Equal(kyc.UserId, notification.UserId);
        Assert.Equal(NotificationType.KycApproved, notification.Type);
    }

    [Fact]
    public async Task NotificationFailureDoesNotReverseKycApproval()
    {
        var unitOfWork = Context(out var kyc);
        kyc.BiometricVerificationUrl = "https://files.test/selfie.jpg";
        kyc.Status = KycStatus.Submitted;
        unitOfWork.NotificationRepository.AddException =
            new InvalidOperationException("notification storage failed");

        var result = await new KycService(
            unitOfWork,
            new NotificationService(unitOfWork))
            .ApproveAsync(kyc.UserId);

        Assert.Equal(KycStatus.Approved, result.Status);
        Assert.Equal(KycStatus.Approved, kyc.Status);
    }

    [Fact]
    public async Task ExistingAdminRejectionStillWorksAndStoresSanitizedReason()
    {
        var unitOfWork = Context(out var kyc);

        var result = await new KycService(unitOfWork).RejectAsync(
            kyc.UserId,
            "  document   mismatch  ");

        Assert.Equal(KycStatus.Rejected, result.Status);
        Assert.Equal("document mismatch", result.RejectionReason);
        var notification = Assert.Single(
            unitOfWork.NotificationRepository.Items);
        Assert.Equal(kyc.UserId, notification.UserId);
        Assert.Equal(NotificationType.KycRejected, notification.Type);
        Assert.Contains("document mismatch", notification.Message);
    }

    [Fact]
    public async Task ExistingMineStatusReturnsOnlyTheAuthenticatedUsersRecord()
    {
        var unitOfWork = Context(out var mine);
        unitOfWork.KycVerificationRepository.Items.Add(new KycVerification
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = KycStatus.Rejected
        });

        var result = await new KycService(unitOfWork)
            .GetMineAsync(mine.UserId);

        Assert.Equal(mine.Id, result.Id);
        Assert.Equal(mine.UserId, result.UserId);
    }

    private static TestUnitOfWork Context(out KycVerification kyc)
    {
        var unitOfWork = new TestUnitOfWork();
        kyc = new KycVerification
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = KycStatus.Pending
        };
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        return unitOfWork;
    }
}
