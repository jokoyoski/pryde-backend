using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/users")]
[Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
public class AdminUsersController(IAdminPortalService adminPortalService) : ControllerBase
{
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Get(Guid userId, CancellationToken cancellationToken) =>
        Ok(await adminPortalService.GetUserAsync(userId, cancellationToken));

    [HttpPatch("{userId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid userId, CancellationToken cancellationToken) =>
        Ok(await adminPortalService.ActivateUserAsync(userId, cancellationToken));

    [HttpPatch("{userId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid userId, CancellationToken cancellationToken) =>
        Ok(await adminPortalService.DeactivateUserAsync(userId, cancellationToken));
}
