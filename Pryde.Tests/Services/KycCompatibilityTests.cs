using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class KycCompatibilityTests
{
    [Fact]
    public async Task CompletedDojahKycCanBeApprovedWithoutManualDocuments()
    {
        var unitOfWork = Context(out var kyc);
        MarkDojahCompleted(kyc);
        unitOfWork.UserRoleRepository.Items.Add(new UserRole
        {
            UserId = kyc.UserId,
            Role = new Role
            {
                Name = RoleType.Driver.ToString()
            }
        });

        var result = await new KycService(unitOfWork)
            .ApproveAsync(kyc.UserId);

        Assert.Null(kyc.BiometricVerificationUrl);
        Assert.Null(kyc.DriverLicenseUrl);
        Assert.Null(kyc.SecondaryIdentificationUrl);
        Assert.Equal(KycStatus.Approved, result.Status);
        Assert.NotNull(result.VerifiedAt);
        Assert.Equal(
            WorkflowNextAction.CompleteVehicleOnboarding,
            result.NextAction);
        Assert.Equal(WorkflowActor.Driver, result.RequiredActor);
        var notification = Assert.Single(
            unitOfWork.NotificationRepository.Items);
        Assert.Equal(kyc.UserId, notification.UserId);
        Assert.Equal(NotificationType.KycApproved, notification.Type);
    }

    [Fact]
    public async Task NotificationFailureDoesNotReverseKycApproval()
    {
        var unitOfWork = Context(out var kyc);
        MarkDojahCompleted(kyc);
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
    public async Task PendingDojahKycCannotBeApproved()
    {
        var unitOfWork = Context(out var kyc);
        kyc.ProviderName = "Dojah";
        kyc.ProviderReference = "PRYDE-current";
        kyc.DojahReference = "DJ-current";
        kyc.ProviderStatus = "Pending";
        kyc.LastProviderUpdatedAt = DateTime.UtcNow;

        await Assert.ThrowsAsync<ConflictException>(() =>
            new KycService(unitOfWork).ApproveAsync(kyc.UserId));

        Assert.Equal(KycStatus.Pending, kyc.Status);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Abandoned")]
    public async Task UnsuccessfulDojahKycCannotBeApproved(
        string providerStatus)
    {
        var unitOfWork = Context(out var kyc);
        kyc.ProviderName = "Dojah";
        kyc.ProviderReference = "PRYDE-current";
        kyc.DojahReference = "DJ-current";
        kyc.ProviderStatus = providerStatus;
        kyc.LastProviderUpdatedAt = DateTime.UtcNow;

        await Assert.ThrowsAsync<ConflictException>(() =>
            new KycService(unitOfWork).ApproveAsync(kyc.UserId));

        Assert.Equal(KycStatus.Pending, kyc.Status);
    }

    [Fact]
    public async Task RetryClearedProviderEvidenceCannotBeApproved()
    {
        var unitOfWork = Context(out var kyc);
        kyc.ProviderName = "Dojah";
        kyc.ProviderReference = "PRYDE-new-attempt";

        await Assert.ThrowsAsync<ConflictException>(() =>
            new KycService(unitOfWork).ApproveAsync(kyc.UserId));

        Assert.Equal(KycStatus.Pending, kyc.Status);
    }

    [Fact]
    public async Task MismatchedDojahAndProviderReferencesCannotBeApproved()
    {
        var unitOfWork = Context(out var kyc);
        MarkDojahCompleted(kyc);
        kyc.DojahReference = kyc.ProviderReference;

        await Assert.ThrowsAsync<ConflictException>(() =>
            new KycService(unitOfWork).ApproveAsync(kyc.UserId));

        Assert.Equal(KycStatus.Pending, kyc.Status);
    }

    [Fact]
    public async Task RepeatedAdminApprovalUsesExistingFinalizedRule()
    {
        var unitOfWork = Context(out var kyc);
        MarkDojahCompleted(kyc);
        var service = new KycService(unitOfWork);

        await service.ApproveAsync(kyc.UserId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.ApproveAsync(kyc.UserId));
        Assert.Equal(KycStatus.Approved, kyc.Status);
        Assert.Single(unitOfWork.NotificationRepository.Items);
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

    private static void MarkDojahCompleted(KycVerification kyc)
    {
        kyc.ProviderName = "Dojah";
        kyc.ProviderReference = $"PRYDE-{Guid.NewGuid():N}";
        kyc.DojahReference = $"DJ-{Guid.NewGuid():N}";
        kyc.ProviderStatus = "Completed";
        kyc.LastProviderUpdatedAt = DateTime.UtcNow;
    }
}
