using Pryde.Domain.Common.Exceptions;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;
using Pryde.Services.Storage.Enums;
using Pryde.Services.Storage.Interface;
using Pryde.Services.Storage.Validation;

namespace Pryde.Services.Service.Implementation;

public class VehicleService(
    IUnitOfWork unitOfWork,
    IFileStorageService fileStorageService,
    IOptions<VehicleUploadSettings> vehicleUploadSettings,
    ILogger<VehicleService> logger,
    INotificationService notificationService) : IVehicleService
{
    private static readonly HashSet<int> AllowedPassengerSeatCounts = [2, 4, 5, 6, 7];
    private static readonly VehicleDocumentType[] RequiredDocumentTypes =
    [
        VehicleDocumentType.VehicleRegistration,
        VehicleDocumentType.Insurance,
        VehicleDocumentType.RoadworthinessCertificate
    ];
    private static readonly string[] AllowedImageContentTypes =
        ["image/jpeg", "image/png", "image/webp"];
    private static readonly string[] AllowedVideoContentTypes =
        ["video/mp4", "video/quicktime", "video/webm"];

    public VehicleService(
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        IOptions<VehicleUploadSettings> vehicleUploadSettings,
        ILogger<VehicleService> logger)
        : this(
            unitOfWork,
            fileStorageService,
            vehicleUploadSettings,
            logger,
            new NotificationService(unitOfWork))
    {
    }

    public async Task<VehicleResponseDto> CreateAsync(
        Guid driverId, string licensePlateNumber, int capacity,
        List<string> imageUrls, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(licensePlateNumber))
            throw new ValidationException("License plate number is required.");
        if (capacity < 0)
            throw new ValidationException("Capacity cannot be negative.");

        var userRoles = await unitOfWork.UserRoles.GetByUserIdAsync(driverId, cancellationToken);
        var isDriver = userRoles.Any(ur => ur.Role.Name == RoleType.Driver.ToString());
        if (!isDriver)
            throw new ForbiddenException("Only users with the Driver role can register a vehicle.");

        var plate = licensePlateNumber.Trim();
        if (await unitOfWork.Vehicles.ExistsAsync(plate, cancellationToken))
            throw new ConflictException("A vehicle with this license plate is already registered.");

        var vehicle = new Vehicle
        {
            UserId = driverId,
            LicensePlateNumber = plate,
            Capacity = capacity,
            PassengerSeatCount = AllowedPassengerSeatCounts.Contains(capacity)
                ? capacity
                : null,
            OnboardingStatus = VehicleOnboardingStatus.Draft,
            IsActive = false
        };

        await unitOfWork.Vehicles.CreateAsync(vehicle, cancellationToken);

        for (var i = 0; i < (imageUrls?.Count ?? 0); i++)
        {
            await unitOfWork.VehicleImages.CreateAsync(new VehicleImage
            {
                VehicleId = vehicle.Id,
                ImageUrl = imageUrls![i].Trim(),
                IsPrimary = i == 0
            }, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CompleteVehicleOnboarding,
            WorkflowActor.Driver,
            cancellationToken);
    }

    public async Task<VehicleResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), id);
        return await BuildResponseAsync(vehicle, cancellationToken);
    }

    public async Task<IReadOnlyList<VehicleResponseDto>> GetMyVehiclesAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        var vehicles = await unitOfWork.Vehicles.GetByUserIdAsync(driverId, cancellationToken);
        var result = new List<VehicleResponseDto>();
        foreach (var vehicle in vehicles)
            result.Add(await BuildResponseAsync(vehicle, cancellationToken));
        return result;
    }

    public async Task<VehicleResponseDto> UpdateAsync(
        Guid vehicleId, Guid requestingUserId, int capacity, CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);

        ValidatePassengerSeatCount(capacity);

        vehicle.Capacity = capacity;
        vehicle.PassengerSeatCount = capacity;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CompleteVehicleOnboarding,
            WorkflowActor.Driver,
            cancellationToken);
    }

    public async Task<VehicleResponseDto> UpdateDetailsAsync(
        Guid vehicleId,
        Guid requestingUserId,
        VehicleDetailsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);
        ValidateVehicleDetails(request);

        vehicle.VehicleOwnerName = NormalizeRequiredText(
            request.VehicleOwnerName, "Vehicle owner name", 200);
        vehicle.RegistrationType = request.RegistrationType;
        vehicle.VehicleType = NormalizeRequiredText(
            request.VehicleType, "Vehicle type", 100);
        vehicle.Make = NormalizeRequiredText(
            request.Make, "Vehicle make", 100);
        vehicle.Model = NormalizeRequiredText(
            request.Model, "Vehicle model", 100);
        vehicle.ManufacturingYear = request.ManufacturingYear;
        vehicle.Colour = NormalizeRequiredText(
            request.Colour, "Vehicle colour", 50);
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CompleteVehicleOnboarding,
            WorkflowActor.Driver,
            cancellationToken);
    }

    public async Task<VehicleResponseDto> UpdateMediaAsync(
        Guid vehicleId,
        Guid requestingUserId,
        IReadOnlyDictionary<VehicleImageType, string> imageUrls,
        string? walkAroundVideoUrl,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);
        if (imageUrls.Count == 0 && string.IsNullOrWhiteSpace(walkAroundVideoUrl))
        {
            throw new ValidationException("At least one vehicle media file is required.");
        }

        var persistedImages = await PersistMediaAsync(
            vehicle,
            imageUrls,
            walkAroundVideoUrl,
            cancellationToken);
        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CompleteVehicleOnboarding,
            WorkflowActor.Driver,
            cancellationToken,
            persistedImages);
    }

    public async Task<VehicleResponseDto> UploadMediaAsync(
        Guid vehicleId,
        Guid requestingUserId,
        VehicleMediaRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var totalStopwatch = Stopwatch.StartNew();
        var uploadedFiles = new ConcurrentBag<UploadedVehicleMedia>();
        var databaseCommitted = false;

        try
        {
            var ownershipStopwatch = Stopwatch.StartNew();
            logger.LogInformation(
                "Vehicle media operation started. VehicleId: {VehicleId}, UserId: {UserId}, Operation: {Operation}",
                vehicleId,
                requestingUserId,
                "VehicleOwnershipLookup");
            var vehicle = await GetEditableOwnedVehicleAsync(
                vehicleId,
                requestingUserId,
                cancellationToken);
            ownershipStopwatch.Stop();
            logger.LogInformation(
                "Vehicle media operation completed. VehicleId: {VehicleId}, UserId: {UserId}, Operation: {Operation}, DurationMilliseconds: {DurationMilliseconds}, Success: {Success}",
                vehicleId,
                requestingUserId,
                "VehicleOwnershipLookup",
                ownershipStopwatch.ElapsedMilliseconds,
                true);

            var validationStopwatch = Stopwatch.StartNew();
            logger.LogInformation(
                "Vehicle media operation started. VehicleId: {VehicleId}, UserId: {UserId}, Operation: {Operation}",
                vehicleId,
                requestingUserId,
                "Validation");
            var pendingFiles = BuildPendingMediaFiles(request);
            if (pendingFiles.Count == 0)
            {
                throw new ValidationException("At least one vehicle media file is required.");
            }

            foreach (var pendingFile in pendingFiles)
            {
                FileUploadValidator.Validate(
                    pendingFile.File,
                    pendingFile.MaximumBytes,
                    pendingFile.AllowedContentTypes,
                    pendingFile.DisplayName);
            }

            validationStopwatch.Stop();
            logger.LogInformation(
                "Vehicle media operation completed. VehicleId: {VehicleId}, UserId: {UserId}, FileCount: {FileCount}, Operation: {Operation}, DurationMilliseconds: {DurationMilliseconds}, Success: {Success}",
                vehicleId,
                requestingUserId,
                pendingFiles.Count,
                "Validation",
                validationStopwatch.ElapsedMilliseconds,
                true);

            var uploadsStopwatch = Stopwatch.StartNew();
            using var uploadCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var uploadTasks = pendingFiles
                .Select(pendingFile => UploadMediaFileAsync(
                    pendingFile,
                    vehicleId,
                    requestingUserId,
                    uploadedFiles,
                    uploadCancellation))
                .ToArray();

            await Task.WhenAll(uploadTasks);
            uploadsStopwatch.Stop();
            logger.LogInformation(
                "Vehicle media operation completed. VehicleId: {VehicleId}, UserId: {UserId}, FileCount: {FileCount}, Operation: {Operation}, DurationMilliseconds: {DurationMilliseconds}, Success: {Success}",
                vehicleId,
                requestingUserId,
                pendingFiles.Count,
                "AllProviderUploads",
                uploadsStopwatch.ElapsedMilliseconds,
                true);

            var imageUrls = uploadedFiles
                .Where(file => file.ImageType.HasValue)
                .ToDictionary(
                    file => file.ImageType!.Value,
                    file => file.PublicUrl);
            var videoUrl = uploadedFiles
                .FirstOrDefault(file => file.Category == FileCategory.VehicleVideo)
                ?.PublicUrl;

            var persistedImages = await PersistMediaAsync(
                vehicle,
                imageUrls,
                videoUrl,
                cancellationToken);
            databaseCommitted = true;

            var response = await BuildWorkflowResponseAsync(
                vehicle,
                WorkflowNextAction.CompleteVehicleOnboarding,
                WorkflowActor.Driver,
                cancellationToken,
                persistedImages);
            totalStopwatch.Stop();
            logger.LogInformation(
                "Vehicle media request completed. VehicleId: {VehicleId}, UserId: {UserId}, FileCount: {FileCount}, Operation: {Operation}, DurationMilliseconds: {DurationMilliseconds}, Success: {Success}",
                vehicleId,
                requestingUserId,
                pendingFiles.Count,
                "TotalRequest",
                totalStopwatch.ElapsedMilliseconds,
                true);
            return response;
        }
        catch
        {
            if (!databaseCommitted && !uploadedFiles.IsEmpty)
            {
                await DeleteUploadedFilesAsync(
                    uploadedFiles,
                    vehicleId,
                    requestingUserId);
            }

            totalStopwatch.Stop();
            logger.LogWarning(
                "Vehicle media request failed. VehicleId: {VehicleId}, UserId: {UserId}, Operation: {Operation}, DurationMilliseconds: {DurationMilliseconds}, Success: {Success}",
                vehicleId,
                requestingUserId,
                "TotalRequest",
                totalStopwatch.ElapsedMilliseconds,
                false);
            throw;
        }
    }

    private async Task<IReadOnlyList<VehicleImage>> PersistMediaAsync(
        Vehicle vehicle,
        IReadOnlyDictionary<VehicleImageType, string> imageUrls,
        string? walkAroundVideoUrl,
        CancellationToken cancellationToken)
    {
        var databaseStopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "Vehicle media operation started. VehicleId: {VehicleId}, UserId: {UserId}, Operation: {Operation}",
            vehicle.Id,
            vehicle.UserId,
            "DatabaseWrite");
        var existingImages = (await unitOfWork.VehicleImages
                .GetByVehicleIdAsync(vehicle.Id, cancellationToken))
            .ToList();
        foreach (var item in imageUrls)
        {
            if (!Enum.IsDefined(item.Key) || string.IsNullOrWhiteSpace(item.Value))
            {
                throw new ValidationException("Vehicle image data is invalid.");
            }

            var existing = existingImages.FirstOrDefault(x => x.ImageType == item.Key);
            if (existing is null)
            {
                var image = new VehicleImage
                {
                    VehicleId = vehicle.Id,
                    ImageType = item.Key,
                    ImageUrl = item.Value.Trim(),
                    IsPrimary = item.Key == VehicleImageType.FrontView
                };
                await unitOfWork.VehicleImages.CreateAsync(
                    image,
                    cancellationToken);
                existingImages.Add(image);
            }
            else
            {
                existing.ImageUrl = item.Value.Trim();
                existing.IsPrimary = item.Key == VehicleImageType.FrontView;
                unitOfWork.VehicleImages.Update(existing);
            }
        }

        if (!string.IsNullOrWhiteSpace(walkAroundVideoUrl))
        {
            vehicle.WalkAroundVideoUrl = walkAroundVideoUrl.Trim();
            unitOfWork.Vehicles.Update(vehicle);
        }

        databaseStopwatch.Stop();
        logger.LogInformation(
            "Vehicle media operation completed. VehicleId: {VehicleId}, UserId: {UserId}, Operation: {Operation}, DurationMilliseconds: {DurationMilliseconds}, Success: {Success}",
            vehicle.Id,
            vehicle.UserId,
            "DatabaseWrite",
            databaseStopwatch.ElapsedMilliseconds,
            true);

        var saveStopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "Vehicle media operation started. VehicleId: {VehicleId}, UserId: {UserId}, Operation: {Operation}",
            vehicle.Id,
            vehicle.UserId,
            "SaveChanges");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        saveStopwatch.Stop();
        logger.LogInformation(
            "Vehicle media operation completed. VehicleId: {VehicleId}, UserId: {UserId}, Operation: {Operation}, DurationMilliseconds: {DurationMilliseconds}, Success: {Success}",
            vehicle.Id,
            vehicle.UserId,
            "SaveChanges",
            saveStopwatch.ElapsedMilliseconds,
            true);
        return existingImages;
    }

    public async Task<VehicleResponseDto> UpdateCapacityExtrasAsync(
        Guid vehicleId,
        Guid requestingUserId,
        VehicleCapacityExtrasRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);
        ValidatePassengerSeatCount(request.PassengerSeatCount);
        if (request.LuggageCapacity.HasValue &&
            !Enum.IsDefined(request.LuggageCapacity.Value))
            throw new ValidationException("Luggage capacity is invalid.");

        var additionalDetails = NormalizeOptionalText(request.AdditionalDetails);
        if (additionalDetails?.Length > 1000)
            throw new ValidationException("Additional details cannot exceed 1000 characters.");

        var requestedAmenities = request.Amenities.Distinct().ToHashSet();
        if (requestedAmenities.Any(x => !Enum.IsDefined(x)))
            throw new ValidationException("One or more vehicle amenities are invalid.");

        var existingAmenities = await unitOfWork.VehicleAmenities
            .GetByVehicleIdAsync(vehicleId, cancellationToken);
        foreach (var existing in existingAmenities
                     .Where(x => !requestedAmenities.Contains(x.AmenityType)))
        {
            unitOfWork.VehicleAmenities.Delete(existing);
        }

        var existingTypes = existingAmenities.Select(x => x.AmenityType).ToHashSet();
        foreach (var amenityType in requestedAmenities.Where(x => !existingTypes.Contains(x)))
        {
            await unitOfWork.VehicleAmenities.CreateAsync(new VehicleAmenity
            {
                VehicleId = vehicleId,
                AmenityType = amenityType
            }, cancellationToken);
        }

        vehicle.PassengerSeatCount = request.PassengerSeatCount;
        vehicle.Capacity = request.PassengerSeatCount;
        if (request.LuggageCapacity.HasValue)
            vehicle.LuggageCapacity = request.LuggageCapacity.Value;
        vehicle.AdditionalDetails = additionalDetails;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CompleteVehicleOnboarding,
            WorkflowActor.Driver,
            cancellationToken);
    }

    public async Task<VehicleResponseDto> SubmitAsync(
        Guid vehicleId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);
        await ValidateCompletenessAsync(vehicle, cancellationToken);

        vehicle.OnboardingStatus = VehicleOnboardingStatus.PendingReview;
        vehicle.IsActive = false;
        vehicle.RejectionReason = null;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.AwaitAdminApproval,
            WorkflowActor.Admin,
            cancellationToken);
    }

    public async Task DeleteAsync(Guid vehicleId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);

        unitOfWork.Vehicles.Delete(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<VehicleResponseDto> AddImagesAsync(
        Guid vehicleId, Guid requestingUserId, List<string> imageUrls, CancellationToken cancellationToken = default)
    {
        var vehicle = await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);

        if (imageUrls is null || imageUrls.Count == 0)
            throw new ValidationException("At least one image is required.");

        var existingImages = await unitOfWork.VehicleImages.GetByVehicleIdAsync(vehicleId, cancellationToken);
        var hasPrimary = existingImages.Any(i => i.IsPrimary);

        for (var i = 0; i < imageUrls.Count; i++)
        {
            await unitOfWork.VehicleImages.CreateAsync(new VehicleImage
            {
                VehicleId = vehicleId,
                ImageUrl = imageUrls[i].Trim(),
                IsPrimary = !hasPrimary && i == 0
            }, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CompleteVehicleOnboarding,
            WorkflowActor.Driver,
            cancellationToken);
    }

    public async Task DeleteImageAsync(Guid vehicleId, Guid imageId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        await GetEditableOwnedVehicleAsync(
            vehicleId, requestingUserId, cancellationToken);

        var images = await unitOfWork.VehicleImages.GetByVehicleIdAsync(vehicleId, cancellationToken);
        if (images.Count <= 1)
            throw new ValidationException("A vehicle must have at least one image — add a replacement before deleting the last one.");

        var image = images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new NotFoundException(nameof(VehicleImage), imageId);

        unitOfWork.VehicleImages.Delete(image);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<VehicleResponseDto> ActivateAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);

        if (vehicle.OnboardingStatus is not (
                VehicleOnboardingStatus.PendingReview or
                VehicleOnboardingStatus.Approved))
        {
            throw new ConflictException(
                "Only a vehicle pending review can be approved.");
        }

        await ValidateRequiredDocumentsApprovedAsync(
            vehicle.Id,
            cancellationToken);

        var kyc = await unitOfWork.KycVerifications
            .GetByUserIdAsync(vehicle.UserId, cancellationToken);
        if (kyc?.Status != KycStatus.Approved)
            throw new ForbiddenException(
                "The vehicle owner must have approved KYC before vehicle activation.");

        if (vehicle.OnboardingStatus == VehicleOnboardingStatus.Approved)
        {
            vehicle.IsActive = true;
            unitOfWork.Vehicles.Update(vehicle);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            var response = await BuildWorkflowResponseAsync(
                vehicle,
                WorkflowNextAction.CreateTrip,
                WorkflowActor.Driver,
                cancellationToken);
            await NotifyVehicleReviewAsync(
                vehicle,
                true,
                null,
                cancellationToken);
            return response;
        }

        await ValidateCompletenessAsync(vehicle, cancellationToken);

        vehicle.OnboardingStatus = VehicleOnboardingStatus.Approved;
        vehicle.IsActive = true;
        vehicle.RejectionReason = null;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var approvalResponse = await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CreateTrip,
            WorkflowActor.Driver,
            cancellationToken);
        await NotifyVehicleReviewAsync(
            vehicle,
            true,
            null,
            cancellationToken);
        return approvalResponse;
    }

    public async Task<VehicleResponseDto> DeactivateAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);
        vehicle.IsActive = false;
        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildResponseAsync(vehicle, cancellationToken);
    }

    public async Task<VehicleResponseDto> RejectAsync(
        Guid vehicleId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(
            vehicleId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);

        if (vehicle.OnboardingStatus != VehicleOnboardingStatus.PendingReview)
            throw new ConflictException("Only a vehicle pending review can be rejected.");

        vehicle.OnboardingStatus = VehicleOnboardingStatus.Rejected;
        vehicle.IsActive = false;
        vehicle.RejectionReason = NormalizeRequiredText(
            reason,
            "Rejection reason",
            500);

        unitOfWork.Vehicles.Update(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var response = await BuildWorkflowResponseAsync(
            vehicle,
            WorkflowNextAction.CompleteVehicleOnboarding,
            WorkflowActor.Driver,
            cancellationToken);
        await NotifyVehicleReviewAsync(
            vehicle,
            false,
            vehicle.RejectionReason,
            cancellationToken);
        return response;
    }

    public async Task<VehicleResponseDto> RejectDriverApplicationAsync(
        Guid driverId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await EnsureDriverAsync(driverId, cancellationToken);
        var vehicles = await unitOfWork.Vehicles.GetByUserIdAsync(
            driverId,
            cancellationToken);
        var pendingVehicle = vehicles
            .Where(vehicle =>
                vehicle.OnboardingStatus ==
                VehicleOnboardingStatus.PendingReview)
            .OrderByDescending(vehicle => vehicle.CreatedAt)
            .FirstOrDefault();

        if (pendingVehicle is null)
        {
            throw new ConflictException(
                "Only a driver application pending review can be rejected.");
        }

        return await RejectAsync(
            pendingVehicle.Id,
            reason,
            cancellationToken);
    }

    private async Task EnsureDriverAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var roles = await unitOfWork.UserRoles.GetByUserIdAsync(
            driverId,
            cancellationToken);
        if (!roles.Any(role =>
                role.Role.Name == RoleType.Driver.ToString()))
        {
            throw new NotFoundException("Driver", driverId);
        }
    }

    private Task NotifyVehicleReviewAsync(
        Vehicle vehicle,
        bool approved,
        string? rejectionReason,
        CancellationToken cancellationToken)
    {
        return notificationService.TryCreateAsync(
            new CreateNotificationRequest
            {
                UserId = vehicle.UserId,
                Type = approved
                    ? NotificationType.VehicleApproved
                    : NotificationType.VehicleRejected,
                Title = approved
                    ? "Vehicle approved"
                    : "Vehicle rejected",
                Message = approved
                    ? "Your vehicle was approved."
                    : $"Your vehicle was rejected: {rejectionReason}",
                RelatedEntityId = vehicle.Id,
                RelatedEntityType = nameof(Vehicle),
                DeduplicationKey = approved
                    ? $"vehicle-approved:{vehicle.Id}"
                    : $"vehicle-rejected:{vehicle.Id}"
            },
            cancellationToken);
    }

    private List<PendingVehicleMedia> BuildPendingMediaFiles(
        VehicleMediaRequestDto request)
    {
        var settings = vehicleUploadSettings.Value;
        var files = new List<PendingVehicleMedia>(5);

        AddPendingImage(
            files,
            request.FrontView,
            VehicleImageType.FrontView,
            settings.VehicleImageMaxBytes,
            "Front view image");
        AddPendingImage(
            files,
            request.RearView,
            VehicleImageType.RearView,
            settings.VehicleImageMaxBytes,
            "Rear view image");
        AddPendingImage(
            files,
            request.SideProfile,
            VehicleImageType.SideProfile,
            settings.VehicleImageMaxBytes,
            "Side profile image");
        AddPendingImage(
            files,
            request.Interior,
            VehicleImageType.Interior,
            settings.VehicleImageMaxBytes,
            "Interior image");

        if (request.WalkAroundVideo is not null)
        {
            files.Add(new PendingVehicleMedia(
                null,
                request.WalkAroundVideo,
                FileCategory.VehicleVideo,
                settings.WalkAroundVideoMaxBytes,
                AllowedVideoContentTypes,
                "Walk-around video"));
        }

        return files;
    }

    private static void AddPendingImage(
        ICollection<PendingVehicleMedia> files,
        IFormFile? file,
        VehicleImageType imageType,
        long maximumBytes,
        string displayName)
    {
        if (file is null)
        {
            return;
        }

        files.Add(new PendingVehicleMedia(
            imageType,
            file,
            FileCategory.VehiclePhoto,
            maximumBytes,
            AllowedImageContentTypes,
            displayName));
    }

    private async Task UploadMediaFileAsync(
        PendingVehicleMedia pendingFile,
        Guid vehicleId,
        Guid requestingUserId,
        ConcurrentBag<UploadedVehicleMedia> uploadedFiles,
        CancellationTokenSource uploadCancellation)
    {
        var uploadStopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "Vehicle media upload started. VehicleId: {VehicleId}, UserId: {UserId}, FileName: {FileName}, FileSizeBytes: {FileSizeBytes}, ContentType: {ContentType}, Operation: {Operation}",
            vehicleId,
            requestingUserId,
            pendingFile.File.FileName,
            pendingFile.File.Length,
            pendingFile.File.ContentType,
            "ProviderUpload");

        try
        {
            await using var stream = pendingFile.File.OpenReadStream();
            var upload = await fileStorageService.UploadAsync(
                stream,
                pendingFile.File.FileName,
                pendingFile.File.ContentType,
                pendingFile.Category,
                requestingUserId,
                uploadCancellation.Token);
            uploadedFiles.Add(new UploadedVehicleMedia(
                pendingFile.ImageType,
                pendingFile.Category,
                upload.FileKey,
                upload.PublicUrl));
            uploadStopwatch.Stop();
            logger.LogInformation(
                "Vehicle media upload completed. VehicleId: {VehicleId}, UserId: {UserId}, FileName: {FileName}, FileSizeBytes: {FileSizeBytes}, ContentType: {ContentType}, Operation: {Operation}, DurationMilliseconds: {DurationMilliseconds}, Success: {Success}",
                vehicleId,
                requestingUserId,
                pendingFile.File.FileName,
                pendingFile.File.Length,
                pendingFile.File.ContentType,
                "ProviderUpload",
                uploadStopwatch.ElapsedMilliseconds,
                true);
        }
        catch
        {
            uploadCancellation.Cancel();
            uploadStopwatch.Stop();
            logger.LogWarning(
                "Vehicle media upload failed. VehicleId: {VehicleId}, UserId: {UserId}, FileName: {FileName}, FileSizeBytes: {FileSizeBytes}, ContentType: {ContentType}, Operation: {Operation}, DurationMilliseconds: {DurationMilliseconds}, Success: {Success}",
                vehicleId,
                requestingUserId,
                pendingFile.File.FileName,
                pendingFile.File.Length,
                pendingFile.File.ContentType,
                "ProviderUpload",
                uploadStopwatch.ElapsedMilliseconds,
                false);
            throw;
        }
    }

    private async Task DeleteUploadedFilesAsync(
        IEnumerable<UploadedVehicleMedia> uploadedFiles,
        Guid vehicleId,
        Guid requestingUserId)
    {
        var deleteTasks = uploadedFiles.Select(async uploadedFile =>
        {
            try
            {
                await fileStorageService.DeleteAsync(
                    uploadedFile.FileKey,
                    uploadedFile.Category,
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Vehicle media cleanup failed. VehicleId: {VehicleId}, UserId: {UserId}, FileKey: {FileKey}, Operation: {Operation}",
                    vehicleId,
                    requestingUserId,
                    uploadedFile.FileKey,
                    "ProviderCleanup");
            }
        });

        await Task.WhenAll(deleteTasks);
    }

    private async Task<VehicleResponseDto> BuildResponseAsync(
        Vehicle vehicle,
        CancellationToken cancellationToken,
        IReadOnlyList<VehicleImage>? loadedImages = null)
    {
        var images = loadedImages ??
            await unitOfWork.VehicleImages.GetByVehicleIdAsync(
                vehicle.Id,
                cancellationToken);
        var amenities = await unitOfWork.VehicleAmenities
            .GetByVehicleIdAsync(vehicle.Id, cancellationToken);
        return new VehicleResponseDto
        {
            Id = vehicle.Id,
            UserId = vehicle.UserId,
            LicensePlateNumber = vehicle.LicensePlateNumber,
            VehicleOwnerName = vehicle.VehicleOwnerName,
            RegistrationType = vehicle.RegistrationType,
            VehicleType = vehicle.VehicleType,
            Make = vehicle.Make,
            Model = vehicle.Model,
            ManufacturingYear = vehicle.ManufacturingYear,
            Colour = vehicle.Colour,
            WalkAroundVideoUrl = vehicle.WalkAroundVideoUrl,
            PassengerSeatCount = vehicle.PassengerSeatCount,
            LuggageCapacity = vehicle.LuggageCapacity,
            Amenities = amenities.Select(x => x.AmenityType).Order().ToList(),
            AdditionalDetails = vehicle.AdditionalDetails,
            OnboardingStatus = vehicle.OnboardingStatus,
            RejectionReason = vehicle.RejectionReason,
            Capacity = vehicle.Capacity,
            IsActive = vehicle.IsActive,
            ImageUrls = images.OrderByDescending(i => i.IsPrimary).Select(i => i.ImageUrl).ToList(),
            Images = images
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.ImageType)
                .Select(i => new VehicleImageResponseDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    ImageType = i.ImageType,
                    IsPrimary = i.IsPrimary
                })
                .ToList()
        };
    }

    private async Task<VehicleResponseDto> BuildWorkflowResponseAsync(
        Vehicle vehicle,
        WorkflowNextAction nextAction,
        WorkflowActor requiredActor,
        CancellationToken cancellationToken,
        IReadOnlyList<VehicleImage>? loadedImages = null)
    {
        var response = await BuildResponseAsync(
            vehicle,
            cancellationToken,
            loadedImages);
        response.WorkflowStatus = vehicle.OnboardingStatus;
        response.NextAction = nextAction;
        response.RequiredActor = requiredActor;
        return response;
    }

    private async Task<Vehicle> GetEditableOwnedVehicleAsync(
        Guid vehicleId,
        Guid requestingUserId,
        CancellationToken cancellationToken)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);
        if (vehicle.UserId != requestingUserId)
            throw new ForbiddenException("You do not have access to this vehicle.");
        if (vehicle.OnboardingStatus is not (
                VehicleOnboardingStatus.Draft or
                VehicleOnboardingStatus.Rejected))
        {
            throw new ConflictException(
                $"A vehicle in {vehicle.OnboardingStatus} status cannot be edited by the driver.");
        }
        return vehicle;
    }

    private async Task ValidateCompletenessAsync(
        Vehicle vehicle,
        CancellationToken cancellationToken)
    {
        var documents = await unitOfWork.VehicleDocuments
            .GetByVehicleIdAsync(vehicle.Id, cancellationToken);
        var images = await unitOfWork.VehicleImages
            .GetByVehicleIdAsync(vehicle.Id, cancellationToken);
        var missingRequirements = new List<string>();

        AddMissingText(
            missingRequirements,
            vehicle.VehicleOwnerName,
            "vehicle owner name");
        if (vehicle.RegistrationType is null ||
            !Enum.IsDefined(vehicle.RegistrationType.Value))
        {
            missingRequirements.Add("registration type");
        }
        AddMissingText(
            missingRequirements,
            vehicle.VehicleType,
            "vehicle type");
        AddMissingText(missingRequirements, vehicle.Make, "make");
        AddMissingText(missingRequirements, vehicle.Model, "model");
        if (!vehicle.ManufacturingYear.HasValue ||
            !IsValidManufacturingYear(vehicle.ManufacturingYear.Value))
        {
            missingRequirements.Add("valid manufacturing year");
        }
        AddMissingText(missingRequirements, vehicle.Colour, "colour");

        foreach (var documentType in RequiredDocumentTypes)
        {
            var document = documents.FirstOrDefault(existing =>
                existing.DocumentType == documentType);
            if (document is null)
            {
                missingRequirements.Add(
                    $"{FormatDocumentType(documentType)} document");
            }
            else if (!document.ExpiryDate.HasValue)
            {
                missingRequirements.Add(
                    $"{FormatDocumentType(documentType)} document expiry date");
            }
        }

        foreach (var imageType in Enum.GetValues<VehicleImageType>())
        {
            if (images.All(image => image.ImageType != imageType))
                missingRequirements.Add($"{FormatImageType(imageType)} photograph");
        }

        if (!vehicle.PassengerSeatCount.HasValue ||
            !AllowedPassengerSeatCounts.Contains(
                vehicle.PassengerSeatCount.Value))
        {
            missingRequirements.Add("valid passenger capacity");
        }

        if (missingRequirements.Count > 0)
        {
            throw new ValidationException(
                $"Vehicle onboarding is incomplete. Missing: {string.Join(", ", missingRequirements)}.");
        }
    }

    private async Task ValidateRequiredDocumentsApprovedAsync(
        Guid vehicleId,
        CancellationToken cancellationToken)
    {
        var documents = await unitOfWork.VehicleDocuments
            .GetByVehicleIdAsync(vehicleId, cancellationToken);
        var missingDocumentTypes = RequiredDocumentTypes
            .Where(documentType => documents.All(document =>
                document.DocumentType != documentType))
            .Select(FormatDocumentType)
            .ToList();
        var unapprovedDocumentTypes = RequiredDocumentTypes
            .Where(documentType => documents.Any(document =>
                document.DocumentType == documentType &&
                document.ReviewStatus != VehicleDocumentReviewStatus.Approved))
            .Select(FormatDocumentType)
            .ToList();

        if (missingDocumentTypes.Count == 0 &&
            unapprovedDocumentTypes.Count == 0)
        {
            return;
        }

        var validationIssues = new List<string>();
        if (missingDocumentTypes.Count > 0)
        {
            validationIssues.Add(
                $"missing: {string.Join(", ", missingDocumentTypes)}");
        }
        if (unapprovedDocumentTypes.Count > 0)
        {
            validationIssues.Add(
                $"not approved: {string.Join(", ", unapprovedDocumentTypes)}");
        }

        throw new ValidationException(
            $"Vehicle activation requires approved VehicleRegistration, Insurance, and RoadworthinessCertificate documents ({string.Join("; ", validationIssues)}).");
    }

    private static string FormatDocumentType(
        VehicleDocumentType documentType) =>
        documentType switch
        {
            VehicleDocumentType.VehicleRegistration => "vehicle registration",
            VehicleDocumentType.RoadworthinessCertificate =>
                "roadworthiness certificate",
            _ => documentType.ToString().ToLowerInvariant()
        };

    private static void ValidateVehicleDetails(
        VehicleDetailsRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = NormalizeRequiredText(
            request.VehicleOwnerName, "Vehicle owner name", 200);
        if (!Enum.IsDefined(request.RegistrationType))
            throw new ValidationException(
                "Vehicle registration type is invalid.");
        _ = NormalizeRequiredText(
            request.VehicleType, "Vehicle type", 100);
        _ = NormalizeRequiredText(
            request.Make, "Vehicle make", 100);
        _ = NormalizeRequiredText(
            request.Model, "Vehicle model", 100);
        if (!IsValidManufacturingYear(request.ManufacturingYear))
        {
            throw new ValidationException(
                $"Manufacturing year must be between 1900 and {DateTime.UtcNow.Year + 1}.");
        }
        _ = NormalizeRequiredText(
            request.Colour, "Vehicle colour", 50);
    }

    private static void ValidatePassengerSeatCount(int passengerSeatCount)
    {
        if (!AllowedPassengerSeatCounts.Contains(passengerSeatCount))
            throw new ValidationException(
                "Passenger seat count must be 2, 4, 5, 6, or 7.");
    }

    private static bool IsValidManufacturingYear(int year) =>
        year is >= 1900 && year <= DateTime.UtcNow.Year + 1;

    private static void AddMissingText(
        ICollection<string> missingRequirements,
        string? value,
        string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            missingRequirements.Add(name);
    }

    private static string FormatImageType(VehicleImageType imageType) =>
        imageType switch
        {
            VehicleImageType.FrontView => "front view",
            VehicleImageType.RearView => "rear view",
            VehicleImageType.SideProfile => "side profile",
            VehicleImageType.Interior => "interior",
            _ => imageType.ToString()
        };

    private static string NormalizeRequiredText(
        string? value,
        string name,
        int maximumLength)
    {
        var normalized = NormalizeOptionalText(value);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ValidationException($"{name} is required.");
        if (normalized.Length > maximumLength)
        {
            throw new ValidationException(
                $"{name} cannot exceed {maximumLength} characters.");
        }
        return normalized;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return string.Join(' ', value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private sealed record PendingVehicleMedia(
        VehicleImageType? ImageType,
        IFormFile File,
        FileCategory Category,
        long MaximumBytes,
        IReadOnlyCollection<string> AllowedContentTypes,
        string DisplayName);

    private sealed record UploadedVehicleMedia(
        VehicleImageType? ImageType,
        FileCategory Category,
        string FileKey,
        string PublicUrl);
}
