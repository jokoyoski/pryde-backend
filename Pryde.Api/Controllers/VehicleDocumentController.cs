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
[Route("api/v{version:apiVersion}/vehicles/{vehicleId:guid}/documents")]
[Authorize]
public class VehicleDocumentController(
    IVehicleDocumentService vehicleDocumentService,
    IFileStorageService fileStorageService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(Guid vehicleId, [FromForm] VehicleDocumentUploadRequestDto request, CancellationToken cancellationToken)
    {
        ValidateFile(request.Document, "Vehicle document");

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
    public async Task<IActionResult> Delete(Guid vehicleId, Guid documentId, CancellationToken cancellationToken)
    {
        await vehicleDocumentService.DeleteAsync(documentId, GetUserId(), cancellationToken);
        return NoContent();
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static void ValidateFile(IFormFile? file, string name)
    {
        if (file is null || file.Length == 0)
            throw new ValidationException($"{name} is required.");
    }
}