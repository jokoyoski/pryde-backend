using Microsoft.AspNetCore.Http;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Storage.Enums;
using Pryde.Services.Storage.Interface;
using Pryde.Services.Storage.Models;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class KycSubmissionServiceTests
{
    [Fact]
    public async Task PassengerSubmittingBiometricOnlySucceeds()
    {
        var unitOfWork = Context(
            out var kyc,
            RoleNames.Passenger);
        kyc.BiometricVerificationUrl = "https://files.test/selfie.jpg";

        var result = await Service(unitOfWork).SubmitAsync(kyc.UserId);

        Assert.Equal(KycStatus.Submitted, result.Status);
    }

    [Fact]
    public async Task PassengerSubmittingWithoutBiometricFails()
    {
        var unitOfWork = Context(
            out var kyc,
            RoleNames.Passenger);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(unitOfWork).SubmitAsync(kyc.UserId));

        Assert.Contains("biometric verification", exception.Message);
    }

    [Fact]
    public async Task PassengerUploadingDriverDocumentsFails()
    {
        var unitOfWork = Context(
            out var kyc,
            RoleNames.Passenger);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Service(unitOfWork, new TestFileStorageService())
                .UploadDocumentsAsync(kyc.UserId, new KycDocumentUploadRequest
                {
                    DriverLicense = File("license.pdf")
                }));
    }

    [Fact]
    public async Task DriverSubmittingAllRequiredDocumentsSucceeds()
    {
        var unitOfWork = Context(
            out var kyc,
            RoleNames.Driver);
        CompleteDriverDocuments(kyc);

        var result = await Service(unitOfWork).SubmitAsync(kyc.UserId);

        Assert.Equal(KycStatus.Submitted, result.Status);
    }

    [Theory]
    [InlineData("biometric")]
    [InlineData("driver's license")]
    [InlineData("secondary identification")]
    public async Task DriverSubmittingWithARequiredDocumentMissingFails(
        string missingDocument)
    {
        var unitOfWork = Context(
            out var kyc,
            RoleNames.Driver);
        CompleteDriverDocuments(kyc);

        if (missingDocument == "biometric")
            kyc.BiometricVerificationUrl = null;
        else if (missingDocument == "driver's license")
            kyc.DriverLicenseUrl = null;
        else
            kyc.SecondaryIdentificationUrl = null;

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(unitOfWork).SubmitAsync(kyc.UserId));

        Assert.Contains(missingDocument, exception.Message);
    }

    [Fact]
    public async Task PassengerAndDriverRolesFollowDriverRequirements()
    {
        var unitOfWork = Context(
            out var kyc,
            RoleNames.Passenger,
            RoleNames.Driver);
        kyc.BiometricVerificationUrl = "https://files.test/selfie.jpg";

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(unitOfWork).SubmitAsync(kyc.UserId));

        Assert.Contains("driver's license", exception.Message);
        Assert.Contains("secondary identification", exception.Message);
    }

    [Fact]
    public async Task AdminCannotApproveIncompleteKyc()
    {
        var unitOfWork = Context(
            out var kyc,
            RoleNames.Driver);
        kyc.Status = KycStatus.Submitted;
        kyc.BiometricVerificationUrl = "https://files.test/selfie.jpg";

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(unitOfWork).ApproveAsync(kyc.UserId));

        Assert.Contains("driver's license", exception.Message);
        Assert.Contains("secondary identification", exception.Message);
    }

    [Fact]
    public async Task AdminCanApproveCompleteSubmittedKyc()
    {
        var unitOfWork = Context(
            out var kyc,
            RoleNames.Driver);
        CompleteDriverDocuments(kyc);
        kyc.Status = KycStatus.Submitted;

        var result = await Service(unitOfWork).ApproveAsync(kyc.UserId);

        Assert.Equal(KycStatus.Approved, result.Status);
        Assert.NotNull(result.VerifiedAt);
    }

    [Fact]
    public async Task IncrementalUploadPreservesExistingDocumentUrls()
    {
        var unitOfWork = Context(
            out var kyc,
            RoleNames.Driver);
        kyc.BiometricVerificationUrl = "https://files.test/existing-selfie.jpg";
        kyc.DriverLicenseUrl = "https://files.test/existing-license.pdf";

        var result = await Service(unitOfWork, new TestFileStorageService())
            .UploadDocumentsAsync(kyc.UserId, new KycDocumentUploadRequest
            {
                SecondaryIdentification = File("secondary.pdf")
            });

        Assert.Equal(
            "https://files.test/existing-selfie.jpg",
            result.BiometricVerificationUrl);
        Assert.Equal(
            "https://files.test/existing-license.pdf",
            result.DriverLicenseUrl);
        Assert.Equal(
            "https://files.test/secondary.pdf",
            result.SecondaryIdentificationUrl);
        Assert.Equal(KycStatus.Pending, result.Status);
    }

    private static TestUnitOfWork Context(
        out KycVerification kyc,
        params string[] roleNames)
    {
        var unitOfWork = new TestUnitOfWork();
        kyc = new KycVerification
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = KycStatus.Pending
        };
        unitOfWork.KycVerificationRepository.Items.Add(kyc);

        foreach (var roleName in roleNames)
        {
            unitOfWork.UserRoleRepository.Items.Add(new UserRole
            {
                UserId = kyc.UserId,
                Role = new Role { Name = roleName }
            });
        }

        return unitOfWork;
    }

    private static void CompleteDriverDocuments(KycVerification kyc)
    {
        kyc.BiometricVerificationUrl = "https://files.test/selfie.jpg";
        kyc.DriverLicenseUrl = "https://files.test/license.pdf";
        kyc.SecondaryIdentificationUrl = "https://files.test/secondary.pdf";
    }

    private static KycService Service(
        TestUnitOfWork unitOfWork,
        IFileStorageService? fileStorageService = null) =>
        new(unitOfWork, fileStorageService!);

    private static IFormFile File(string fileName)
    {
        var content = new MemoryStream([1, 2, 3]);
        return new FormFile(
            content,
            0,
            content.Length,
            "file",
            fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }

    private sealed class TestFileStorageService : IFileStorageService
    {
        public Task<FileUploadResult> UploadAsync(
            Stream fileStream,
            string fileName,
            string contentType,
            FileCategory category,
            Guid ownerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FileUploadResult(
                $"kyc/{fileName}",
                $"https://files.test/{fileName}"));

        public Task<string> GetReadUrlAsync(
            string fileKey,
            FileCategory category,
            CancellationToken cancellationToken = default) =>
            Task.FromResult($"https://files.test/{fileKey}");

        public Task DeleteAsync(
            string fileKey,
            FileCategory category,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
