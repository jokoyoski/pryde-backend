using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pryde.Api.Authorization;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;
using Pryde.Services.Storage.Enums;
using Pryde.Services.Storage.Interface;
namespace Pryde.Api.Controllers.V1;
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vehicles/{vehicleId:guid}/documents")]
[Authorize]
public class VehicleDocumentController(
    IVehicleDocumentService vehicleDocumentService,
    IFileStorageService fileStorageService,
    IAdminListingService adminListingService,
    IOptions<VehicleUploadSettings> vehicleUploadSettings) : ControllerBase
{
    [HttpGet("~/api/v{version:apiVersion}/admin/vehicle-documents")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> GetAdminVehicleDocuments(
        [FromQuery] AdminVehicleDocumentsRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await adminListingService.GetVehicleDocumentsAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("~/api/v{version:apiVersion}/admin/vehicle-documents/{documentId:guid}")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> GetAdminVehicleDocument(
        Guid documentId, CancellationToken cancellationToken)
    {
        return Ok(await vehicleDocumentService.GetForAdminAsync(documentId, cancellationToken));
    }

    [HttpPatch("~/api/v{version:apiVersion}/admin/vehicle-documents/{documentId:guid}/approve")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> Approve(
        Guid documentId, CancellationToken cancellationToken)
    {
        return Ok(await vehicleDocumentService.ApproveAsync(
            documentId, GetUserId(), cancellationToken));
    }

    [HttpPatch("~/api/v{version:apiVersion}/admin/vehicle-documents/{documentId:guid}/reject")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> Reject(
        Guid documentId, [FromBody] RejectionRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await vehicleDocumentService.RejectAsync(
            documentId, GetUserId(), request.Reason, cancellationToken));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> Upload(Guid vehicleId, [FromForm] VehicleDocumentUploadRequestDto request, CancellationToken cancellationToken)
    {
        ValidateFile(
            request.Document,
            vehicleUploadSettings.Value.VehicleDocumentMaxBytes,
            "Vehicle document");

        var userId = GetUserId();
        await using var stream = request.Document!.OpenReadStream();
        var upload = await fileStorageService.UploadAsync(
            stream, request.Document.FileName, request.Document.ContentType,
            FileCategory.VehicleDocument, userId, cancellationToken);

        var result = await vehicleDocumentService.UploadAsync(vehicleId, userId, request.DocumentType, request.ExpiryDate, upload.PublicUrl, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetByVehicle(Guid vehicleId, CancellationToken cancellationToken)
    {
        var result = await vehicleDocumentService.GetByVehicleIdAsync(vehicleId, GetUserId(), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{documentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> Delete(Guid vehicleId, Guid documentId, CancellationToken cancellationToken)
    {
        await vehicleDocumentService.DeleteAsync(documentId, GetUserId(), cancellationToken);
        return NoContent();
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static void ValidateFile(
        IFormFile? file,
        long maximumBytes,
        string name)
    {
        if (file is null || file.Length == 0)
            throw new ValidationException($"{name} is required.");
        if (file.Length > maximumBytes)
            throw new ValidationException($"{name} exceeds the configured upload limit.");
        var allowedContentTypes = new[]
        {
            "application/pdf", "image/jpeg", "image/png", "image/webp"
        };
        if (!allowedContentTypes.Contains(
                file.ContentType,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException($"{name} file type is not supported.");
        }
    }
}
