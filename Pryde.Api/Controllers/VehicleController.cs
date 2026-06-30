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
    IFileStorageService fileStorageService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] VehicleCreateRequestDto request, CancellationToken cancellationToken)
    {
        ValidateFile(request.VehicleImage, "Vehicle image");

        var userId = GetUserId();
        await using var stream = request.VehicleImage!.OpenReadStream();
        var upload = await fileStorageService.UploadAsync(
            stream, request.VehicleImage.FileName, request.VehicleImage.ContentType,
            FileCategory.VehiclePhoto, userId, cancellationToken);

        var result = await vehicleService.CreateAsync(userId, request.LicensePlateNumber, request.Capacity, upload.PublicUrl, cancellationToken);
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
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(Guid id, [FromForm] VehicleUpdateRequestDto request, CancellationToken cancellationToken)
    {
        ValidateFile(request.VehicleImage, "Vehicle image");

        var userId = GetUserId();
        await using var stream = request.VehicleImage!.OpenReadStream();
        var upload = await fileStorageService.UploadAsync(
            stream, request.VehicleImage.FileName, request.VehicleImage.ContentType,
            FileCategory.VehiclePhoto, userId, cancellationToken);

        var result = await vehicleService.UpdateAsync(id, userId, request.Capacity, upload.PublicUrl, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await vehicleService.DeleteAsync(id, GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await vehicleService.ActivateAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await vehicleService.DeactivateAsync(id, cancellationToken);
        return Ok(result);
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static void ValidateFile(IFormFile? file, string name)
    {
        if (file is null || file.Length == 0)
            throw new ValidationException($"{name} is required.");
    }
}