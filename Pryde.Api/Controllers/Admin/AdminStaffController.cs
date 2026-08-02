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
[Route("api/v{version:apiVersion}/admin/staff")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class AdminStaffController(IAdminPortalService adminPortalService) : ControllerBase
{
    [HttpPost("invite")]
    public async Task<IActionResult> Invite(
        [FromBody] InviteStaffRequestDto request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created,
            await adminPortalService.InviteStaffAsync(request, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] AdminStaffRequestDto request, CancellationToken cancellationToken) =>
        Ok(await adminPortalService.GetStaffAsync(request, cancellationToken));

    [HttpGet("{staffId:guid}")]
    public async Task<IActionResult> GetById(Guid staffId, CancellationToken cancellationToken) =>
        Ok(await adminPortalService.GetStaffByIdAsync(staffId, cancellationToken));

    [HttpPatch("{staffId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid staffId, CancellationToken cancellationToken) =>
        Ok(await adminPortalService.ActivateStaffAsync(staffId, cancellationToken));

    [HttpPatch("{staffId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid staffId, CancellationToken cancellationToken) =>
        Ok(await adminPortalService.DeactivateStaffAsync(staffId, GetUserId(), cancellationToken));

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
