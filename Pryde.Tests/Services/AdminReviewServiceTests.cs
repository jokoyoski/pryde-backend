using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class AdminReviewServiceTests
{
    [Fact]
    public async Task KycCannotBeFinalizedTwiceAndRejectionRequiresReason()
    {
        var unitOfWork = new TestUnitOfWork();
        var kyc = new KycVerification
        {
            UserId = Guid.NewGuid(),
            Status = KycStatus.Submitted,
            BiometricVerificationUrl = "https://files.test/selfie.jpg"
        };
        unitOfWork.KycVerificationRepository.Items.Add(kyc);
        var service = new KycService(unitOfWork);

        await Assert.ThrowsAsync<ValidationException>(() => service.RejectAsync(kyc.UserId, " "));
        await service.ApproveAsync(kyc.UserId);
        await Assert.ThrowsAsync<ConflictException>(() => service.RejectAsync(kyc.UserId, "changed"));
    }

    [Fact]
    public async Task VehicleDocumentReviewStoresReviewerAndCannotFinalizeTwice()
    {
        var unitOfWork = new TestUnitOfWork();
        var document = new VehicleDocument
        {
            VehicleId = Guid.NewGuid(),
            DocumentUrl = "https://files.test/document.pdf",
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            ReviewStatus = VehicleDocumentReviewStatus.Pending
        };
        ((TestVehicleDocumentRepository)unitOfWork.VehicleDocuments).Items.Add(document);
        var reviewer = Guid.NewGuid();
        var service = new VehicleDocumentService(unitOfWork);

        var result = await service.ApproveAsync(document.Id, reviewer);

        Assert.Equal(VehicleDocumentReviewStatus.Approved, result.ReviewStatus);
        Assert.Equal(reviewer, result.ReviewedBy);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RejectAsync(document.Id, reviewer, "invalid"));
    }
}
