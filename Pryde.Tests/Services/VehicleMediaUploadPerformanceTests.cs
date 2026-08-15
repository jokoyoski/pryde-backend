using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pryde.Api.Middleware;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Settings;
using Pryde.Services.Storage.Enums;
using Pryde.Services.Storage.Interface;
using Pryde.Services.Storage.Models;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class VehicleMediaUploadPerformanceTests
{
    [Fact]
    public async Task UploadMediaAsync_UploadsFourValidImagesConcurrently_AndSavesOnce()
    {
        var context = Context();
        var storage = new TrackingFileStorageService(
            TimeSpan.FromMilliseconds(75));
        var service = Service(context.UnitOfWork, storage);

        var result = await service.UploadMediaAsync(
            context.Vehicle.Id,
            context.OwnerId,
            FourImageRequest());

        Assert.Equal(4, storage.UploadedFileNames.Count);
        Assert.True(storage.MaximumConcurrentUploads > 1);
        Assert.Equal(4, context.UnitOfWork.VehicleImageRepository.Items.Count);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCount);
        Assert.Equal(4, result.Images.Count);
        Assert.Equal(
            Enum.GetValues<VehicleImageType>().Order(),
            result.Images.Select(image => image.ImageType!.Value).Order());
        Assert.Contains(
            result.Images,
            image => image.ImageType == VehicleImageType.FrontView &&
                     image.ImageUrl.EndsWith(
                         "/front.jpg",
                         StringComparison.Ordinal));
    }

    [Fact]
    public async Task UploadMediaAsync_ValidatesEveryFileBeforeAnyUploadStarts()
    {
        var context = Context();
        var storage = new TrackingFileStorageService();
        var service = Service(context.UnitOfWork, storage);
        var request = new VehicleMediaRequestDto
        {
            FrontView = Image("front.jpg"),
            RearView = File(
                "rear.exe",
                "application/octet-stream")
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UploadMediaAsync(
                context.Vehicle.Id,
                context.OwnerId,
                request));

        Assert.Empty(storage.UploadedFileNames);
        Assert.Equal(0, context.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task UploadMediaAsync_RejectsWrongOwnerBeforeAnyUploadStarts()
    {
        var context = Context();
        var storage = new TrackingFileStorageService();
        var service = Service(context.UnitOfWork, storage);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.UploadMediaAsync(
                context.Vehicle.Id,
                Guid.NewGuid(),
                FourImageRequest()));

        Assert.Empty(storage.UploadedFileNames);
        Assert.Equal(0, context.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task UploadMediaAsync_RejectsNonEditableVehicleBeforeAnyUploadStarts()
    {
        var context = Context(VehicleOnboardingStatus.PendingReview);
        var storage = new TrackingFileStorageService();
        var service = Service(context.UnitOfWork, storage);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UploadMediaAsync(
                context.Vehicle.Id,
                context.OwnerId,
                FourImageRequest()));

        Assert.Empty(storage.UploadedFileNames);
        Assert.Equal(0, context.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task UploadMediaAsync_FailedUploadDoesNotPersistAndKeepsExistingMedia()
    {
        var context = Context();
        const string existingUrl = "https://files.test/existing-front.jpg";
        context.UnitOfWork.VehicleImageRepository.Items.Add(new VehicleImage
        {
            VehicleId = context.Vehicle.Id,
            ImageType = VehicleImageType.FrontView,
            ImageUrl = existingUrl,
            IsPrimary = true
        });
        var storage = new TrackingFileStorageService(
            TimeSpan.FromMilliseconds(5),
            "rear.jpg",
            TimeSpan.FromMilliseconds(40));
        var service = Service(context.UnitOfWork, storage);
        var request = new VehicleMediaRequestDto
        {
            FrontView = Image("front.jpg"),
            RearView = Image("rear.jpg")
        };

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            service.UploadMediaAsync(
                context.Vehicle.Id,
                context.OwnerId,
                request));

        Assert.Equal(0, context.UnitOfWork.SaveChangesCount);
        Assert.Single(context.UnitOfWork.VehicleImageRepository.Items);
        Assert.Equal(
            existingUrl,
            context.UnitOfWork.VehicleImageRepository.Items[0].ImageUrl);
        Assert.Contains(
            storage.DeletedFileKeys,
            key => key.Contains("front.jpg", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UploadMediaAsync_CancellationStopsOutstandingUploadsWithoutSaving()
    {
        var context = Context();
        var storage = new TrackingFileStorageService(
            TimeSpan.FromSeconds(1));
        var service = Service(context.UnitOfWork, storage);
        using var cancellation = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.UploadMediaAsync(
                context.Vehicle.Id,
                context.OwnerId,
                FourImageRequest(),
                cancellation.Token));

        Assert.Equal(0, context.UnitOfWork.SaveChangesCount);
        Assert.Empty(context.UnitOfWork.VehicleImageRepository.Items);
        Assert.Equal(0, storage.CurrentConcurrentUploads);
    }

    [Fact]
    public async Task UploadMediaAsync_ProviderUnavailableIsPreservedFor503Middleware()
    {
        var context = Context();
        var storage = new TrackingFileStorageService(
            failureFileName: "front.jpg");
        var service = Service(context.UnitOfWork, storage);
        var request = new VehicleMediaRequestDto
        {
            FrontView = Image("front.jpg")
        };

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            service.UploadMediaAsync(
                context.Vehicle.Id,
                context.OwnerId,
                request));

        Assert.Equal(0, context.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExceptionMiddleware_MapsStorageUnavailableTo503()
    {
        var middleware = new ExceptionMiddleware(
            _ => throw new ServiceUnavailableException(
                "File storage is temporarily unavailable."),
            NullLogger<ExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            context.Response.StatusCode);
    }

    [Fact]
    public async Task VehicleApprovalNotifiesOwner()
    {
        var context = Context(VehicleOnboardingStatus.Approved);
        context.UnitOfWork.KycVerificationRepository.Items.Add(
            new KycVerification
            {
                UserId = context.OwnerId,
                Status = KycStatus.Approved
            });
        foreach (var documentType in new[]
                 {
                     VehicleDocumentType.VehicleRegistration,
                     VehicleDocumentType.Insurance,
                     VehicleDocumentType.RoadworthinessCertificate
                 })
        {
            context.UnitOfWork.VehicleDocumentRepository.Items.Add(
                new VehicleDocument
                {
                    VehicleId = context.Vehicle.Id,
                    DocumentType = documentType,
                    DocumentUrl = $"https://files.test/{documentType}.pdf",
                    ExpiryDate = DateTime.UtcNow.AddYears(1),
                    ReviewStatus = VehicleDocumentReviewStatus.Approved
                });
        }

        await Service(
            context.UnitOfWork,
            new TrackingFileStorageService())
            .ActivateAsync(context.Vehicle.Id);

        var notification = Assert.Single(
            context.UnitOfWork.NotificationRepository.Items);
        Assert.Equal(context.OwnerId, notification.UserId);
        Assert.Equal(NotificationType.VehicleApproved, notification.Type);
    }

    [Fact]
    public async Task VehicleRejectionNotifiesOwner()
    {
        var context = Context(VehicleOnboardingStatus.PendingReview);

        await Service(
            context.UnitOfWork,
            new TrackingFileStorageService())
            .RejectAsync(context.Vehicle.Id, "Documents are unclear.");

        var notification = Assert.Single(
            context.UnitOfWork.NotificationRepository.Items);
        Assert.Equal(context.OwnerId, notification.UserId);
        Assert.Equal(NotificationType.VehicleRejected, notification.Type);
    }

    private static VehicleService Service(
        TestUnitOfWork unitOfWork,
        IFileStorageService storage)
    {
        return new VehicleService(
            unitOfWork,
            storage,
            Options.Create(new VehicleUploadSettings()),
            NullLogger<VehicleService>.Instance);
    }

    private static (
        TestUnitOfWork UnitOfWork,
        Guid OwnerId,
        Vehicle Vehicle) Context(
        VehicleOnboardingStatus status = VehicleOnboardingStatus.Draft)
    {
        var unitOfWork = new TestUnitOfWork();
        var ownerId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            UserId = ownerId,
            LicensePlateNumber = "PRYDE-MEDIA",
            OnboardingStatus = status
        };
        unitOfWork.VehicleRepository.Items.Add(vehicle);
        return (unitOfWork, ownerId, vehicle);
    }

    private static VehicleMediaRequestDto FourImageRequest()
    {
        return new VehicleMediaRequestDto
        {
            FrontView = Image("front.jpg"),
            RearView = Image("rear.jpg"),
            SideProfile = Image("side.jpg"),
            Interior = Image("interior.jpg")
        };
    }

    private static IFormFile Image(string fileName)
    {
        return File(fileName, "image/jpeg");
    }

    private static IFormFile File(
        string fileName,
        string contentType)
    {
        var bytes = new byte[128];
        return new FormFile(
            new MemoryStream(bytes),
            0,
            bytes.Length,
            "file",
            fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class TrackingFileStorageService(
        TimeSpan? uploadDelay = null,
        string? failureFileName = null,
        TimeSpan? failureDelay = null) : IFileStorageService
    {
        private int _currentConcurrentUploads;
        private int _maximumConcurrentUploads;

        public List<string> UploadedFileNames { get; } = [];
        public List<string> DeletedFileKeys { get; } = [];
        public int CurrentConcurrentUploads =>
            Volatile.Read(ref _currentConcurrentUploads);
        public int MaximumConcurrentUploads =>
            Volatile.Read(ref _maximumConcurrentUploads);

        public async Task<FileUploadResult> UploadAsync(
            Stream fileStream,
            string fileName,
            string contentType,
            FileCategory category,
            Guid ownerId,
            CancellationToken cancellationToken = default)
        {
            lock (UploadedFileNames)
            {
                UploadedFileNames.Add(fileName);
            }

            var current = Interlocked.Increment(
                ref _currentConcurrentUploads);
            UpdateMaximumConcurrency(current);

            try
            {
                var delay = string.Equals(
                    fileName,
                    failureFileName,
                    StringComparison.Ordinal)
                    ? failureDelay ?? uploadDelay ?? TimeSpan.Zero
                    : uploadDelay ?? TimeSpan.Zero;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }

                if (string.Equals(
                    fileName,
                    failureFileName,
                    StringComparison.Ordinal))
                {
                    throw new ServiceUnavailableException(
                        "File storage is temporarily unavailable.");
                }

                return new FileUploadResult(
                    $"key/{fileName}",
                    $"https://files.test/{fileName}");
            }
            finally
            {
                Interlocked.Decrement(ref _currentConcurrentUploads);
            }
        }

        public Task<string> GetReadUrlAsync(
            string fileKey,
            FileCategory category,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"https://files.test/{fileKey}");
        }

        public Task DeleteAsync(
            string fileKey,
            FileCategory category,
            CancellationToken cancellationToken = default)
        {
            lock (DeletedFileKeys)
            {
                DeletedFileKeys.Add(fileKey);
            }

            return Task.CompletedTask;
        }

        private void UpdateMaximumConcurrency(int current)
        {
            while (true)
            {
                var maximum = Volatile.Read(
                    ref _maximumConcurrentUploads);
                if (current <= maximum)
                {
                    return;
                }

                if (Interlocked.CompareExchange(
                        ref _maximumConcurrentUploads,
                        current,
                        maximum) == maximum)
                {
                    return;
                }
            }
        }
    }
}
