using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/drivers")]
[Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
public class AdminDriversController(
    IAdminPortalService adminPortalService,
    IVehicleService vehicleService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] AdminDriversRequestDto request, CancellationToken cancellationToken) =>
        Ok(await adminPortalService.GetDriversAsync(request, cancellationToken));

    [HttpGet("{driverId:guid}")]
    public async Task<IActionResult> Get(Guid driverId, CancellationToken cancellationToken) =>
        Ok(await adminPortalService.GetDriverAsync(driverId, cancellationToken));

    [HttpPatch("{driverId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid driverId, CancellationToken cancellationToken) =>
        Ok(await adminPortalService.ActivateDriverAsync(driverId, cancellationToken));

    [HttpPatch("{driverId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid driverId, CancellationToken cancellationToken) =>
        Ok(await adminPortalService.DeactivateDriverAsync(driverId, cancellationToken));

    [HttpPatch("{driverId:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid driverId,
        [FromBody] RejectionRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await vehicleService.RejectDriverApplicationAsync(
            driverId,
            request.Reason,
            cancellationToken));
}
