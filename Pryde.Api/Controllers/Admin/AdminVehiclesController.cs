using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/vehicles")]
[Authorize]
public class AdminVehiclesController(
    IVehicleService vehicleService,
    IAdminListingService adminListingService,
    IAdminPortalService adminPortalService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> GetAdminVehicles(
        [FromQuery] AdminVehiclesRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await adminListingService.GetVehiclesAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> GetAdminVehicle(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await adminPortalService.GetVehicleAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> ActivateVehicle(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await vehicleService.ActivateAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> DeactivateVehicle(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await vehicleService.DeactivateAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{vehicleId:guid}/reject")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> RejectVehicle(
        Guid vehicleId,
        [FromBody] RejectionRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await vehicleService.RejectAsync(
            vehicleId,
            request.Reason,
            cancellationToken);
        return Ok(result);
    }
}
