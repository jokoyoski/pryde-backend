using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Services.Service.Interface;
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
    IAdminPortalService adminPortalService) : ControllerBase
{
    [HttpGet("~/api/v{version:apiVersion}/admin/vehicles")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetAdminVehicles(
        [FromQuery] AdminVehiclesRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await adminListingService.GetVehiclesAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("~/api/v{version:apiVersion}/admin/vehicles/{id:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetAdminVehicle(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await adminPortalService.GetVehicleAsync(id, cancellationToken));
    }

    [HttpPost("~/api/v{version:apiVersion}/admin/vehicles/{id:guid}/activate")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> ActivateVehicle(Guid id, CancellationToken cancellationToken)
    {
        var result = await vehicleService.ActivateAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("~/api/v{version:apiVersion}/admin/vehicles/{id:guid}/deactivate")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> DeactivateVehicle(Guid id, CancellationToken cancellationToken)
    {
        var result = await vehicleService.DeactivateAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] VehicleCreateRequestDto request, CancellationToken cancellationToken)
    {
        if (request.VehicleImages is null || request.VehicleImages.Count == 0)
            throw new ValidationException("At least one vehicle image is required.");

        var userId = GetUserId();
        var imageUrls = await UploadImagesAsync(request.VehicleImages, userId, cancellationToken);

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
    public async Task<IActionResult> Update(Guid id, [FromBody] VehicleUpdateRequestDto request, CancellationToken cancellationToken)
    {
        var result = await vehicleService.UpdateAsync(id, GetUserId(), request.Capacity, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AddImages(Guid id, [FromForm] List<IFormFile> images, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var imageUrls = await UploadImagesAsync(images, userId, cancellationToken);
        var result = await vehicleService.AddImagesAsync(id, userId, imageUrls, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        await vehicleService.DeleteImageAsync(id, imageId, GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await vehicleService.DeleteAsync(id, GetUserId(), cancellationToken);
        return NoContent();
    }
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<List<string>> UploadImagesAsync(List<IFormFile> files, Guid userId, CancellationToken cancellationToken)
    {
        var urls = new List<string>();
        foreach (var file in files)
        {
            if (file is null || file.Length == 0)
                throw new ValidationException("One of the uploaded images is empty.");

            await using var stream = file.OpenReadStream();
            var upload = await fileStorageService.UploadAsync(
                stream, file.FileName, file.ContentType, FileCategory.VehiclePhoto, userId, cancellationToken);
            urls.Add(upload.PublicUrl);
        }
        return urls;
    }
}
