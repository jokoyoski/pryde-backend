using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pryde.Api.Authorization;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Constants;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;
using Pryde.Services.Storage.Enums;
using Pryde.Services.Storage.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vehicles")]
[Authorize]
public class VehicleController(
    IVehicleService vehicleService,
    IFileStorageService fileStorageService,
    IAdminListingService adminListingService,
    IAdminPortalService adminPortalService,
    IOptions<VehicleUploadSettings> vehicleUploadSettings) : ControllerBase
{
    [HttpGet("~/api/v{version:apiVersion}/admin/vehicles")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> GetAdminVehicles(
        [FromQuery] AdminVehiclesRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await adminListingService.GetVehiclesAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("~/api/v{version:apiVersion}/admin/vehicles/{id:guid}")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> GetAdminVehicle(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await adminPortalService.GetVehicleAsync(id, cancellationToken));
    }

    [HttpPost("~/api/v{version:apiVersion}/admin/vehicles/{id:guid}/activate")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> ActivateVehicle(Guid id, CancellationToken cancellationToken)
    {
        var result = await vehicleService.ActivateAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("~/api/v{version:apiVersion}/admin/vehicles/{id:guid}/deactivate")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> DeactivateVehicle(Guid id, CancellationToken cancellationToken)
    {
        var result = await vehicleService.DeactivateAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> Create([FromForm] VehicleCreateRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var imageUrls = request.VehicleImages.Count == 0
            ? []
            : await UploadImagesAsync(request.VehicleImages, userId, cancellationToken);

        var result = await vehicleService.CreateAsync(userId, request.LicensePlateNumber, request.Capacity, imageUrls, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var result = await vehicleService.GetMyVehiclesAsync(GetUserId(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await vehicleService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> Update(Guid id, [FromBody] VehicleUpdateRequestDto request, CancellationToken cancellationToken)
    {
        var result = await vehicleService.UpdateAsync(id, GetUserId(), request.Capacity, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> AddImages(Guid id, [FromForm] List<IFormFile> images, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var imageUrls = await UploadImagesAsync(images, userId, cancellationToken);
        var result = await vehicleService.AddImagesAsync(id, userId, imageUrls, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        await vehicleService.DeleteImageAsync(id, imageId, GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await vehicleService.DeleteAsync(id, GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpPut("{vehicleId:guid}/details")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> UpdateDetails(
        Guid vehicleId,
        [FromBody] VehicleDetailsRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await vehicleService.UpdateDetailsAsync(
            vehicleId, GetUserId(), request, cancellationToken));
    }

    [HttpPut("{vehicleId:guid}/media")]
    [Consumes("multipart/form-data")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> UpdateMedia(
        Guid vehicleId,
        [FromForm] VehicleMediaRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var imageUrls = new Dictionary<VehicleImageType, string>();
        await AddTypedImageAsync(
            imageUrls, VehicleImageType.FrontView, request.FrontView,
            userId, cancellationToken);
        await AddTypedImageAsync(
            imageUrls, VehicleImageType.RearView, request.RearView,
            userId, cancellationToken);
        await AddTypedImageAsync(
            imageUrls, VehicleImageType.SideProfile, request.SideProfile,
            userId, cancellationToken);
        await AddTypedImageAsync(
            imageUrls, VehicleImageType.Interior, request.Interior,
            userId, cancellationToken);

        string? videoUrl = null;
        if (request.WalkAroundVideo is not null)
        {
            ValidateFile(
                request.WalkAroundVideo,
                vehicleUploadSettings.Value.WalkAroundVideoMaxBytes,
                ["video/mp4", "video/quicktime", "video/webm"],
                "Walk-around video");
            await using var stream = request.WalkAroundVideo.OpenReadStream();
            var upload = await fileStorageService.UploadAsync(
                stream,
                request.WalkAroundVideo.FileName,
                request.WalkAroundVideo.ContentType,
                FileCategory.VehicleVideo,
                userId,
                cancellationToken);
            videoUrl = upload.PublicUrl;
        }

        return Ok(await vehicleService.UpdateMediaAsync(
            vehicleId, userId, imageUrls, videoUrl, cancellationToken));
    }

    [HttpPut("{vehicleId:guid}/capacity-extras")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> UpdateCapacityExtras(
        Guid vehicleId,
        [FromBody] VehicleCapacityExtrasRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await vehicleService.UpdateCapacityExtrasAsync(
            vehicleId, GetUserId(), request, cancellationToken));
    }

    [HttpPost("{vehicleId:guid}/submit")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> Submit(
        Guid vehicleId,
        CancellationToken cancellationToken)
    {
        return Ok(await vehicleService.SubmitAsync(
            vehicleId, GetUserId(), cancellationToken));
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<List<string>> UploadImagesAsync(List<IFormFile> files, Guid userId, CancellationToken cancellationToken)
    {
        var urls = new List<string>();
        foreach (var file in files)
        {
            ValidateFile(
                file,
                vehicleUploadSettings.Value.VehicleImageMaxBytes,
                ["image/jpeg", "image/png", "image/webp"],
                "Vehicle image");

            await using var stream = file.OpenReadStream();
            var upload = await fileStorageService.UploadAsync(
                stream, file.FileName, file.ContentType, FileCategory.VehiclePhoto, userId, cancellationToken);
            urls.Add(upload.PublicUrl);
        }
        return urls;
    }

    private async Task AddTypedImageAsync(
        IDictionary<VehicleImageType, string> urls,
        VehicleImageType imageType,
        IFormFile? file,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return;
        ValidateFile(
            file,
            vehicleUploadSettings.Value.VehicleImageMaxBytes,
            ["image/jpeg", "image/png", "image/webp"],
            imageType.ToString());
        await using var stream = file.OpenReadStream();
        var upload = await fileStorageService.UploadAsync(
            stream,
            file.FileName,
            file.ContentType,
            FileCategory.VehiclePhoto,
            userId,
            cancellationToken);
        urls[imageType] = upload.PublicUrl;
    }

    private static void ValidateFile(
        IFormFile file,
        long maximumBytes,
        IReadOnlyCollection<string> allowedContentTypes,
        string name)
    {
        if (file.Length == 0)
            throw new ValidationException($"{name} is empty.");
        if (file.Length > maximumBytes)
            throw new ValidationException($"{name} exceeds the configured upload limit.");
        if (!allowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new ValidationException($"{name} file type is not supported.");
    }
}
