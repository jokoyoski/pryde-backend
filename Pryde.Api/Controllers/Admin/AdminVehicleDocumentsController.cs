using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/vehicle-documents")]
[Authorize]
public class AdminVehicleDocumentsController(
    IVehicleDocumentService vehicleDocumentService,
    IAdminListingService adminListingService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> GetAdminVehicleDocuments(
        [FromQuery] AdminVehicleDocumentsRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await adminListingService.GetVehicleDocumentsAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{documentId:guid}")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> GetAdminVehicleDocument(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        return Ok(await vehicleDocumentService.GetForAdminAsync(documentId, cancellationToken));
    }

    [HttpPatch("{documentId:guid}/approve")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> Approve(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        return Ok(await vehicleDocumentService.ApproveAsync(
            documentId,
            GetUserId(),
            cancellationToken));
    }

    [HttpPatch("{documentId:guid}/reject")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> Reject(
        Guid documentId,
        [FromBody] RejectionRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await vehicleDocumentService.RejectAsync(
            documentId,
            GetUserId(),
            request.Reason,
            cancellationToken));
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
