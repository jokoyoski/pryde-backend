using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/dashboard")]
[Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
public class AdminDashboardController(IAdminPortalService adminPortalService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int days = 7,CancellationToken cancellationToken = default)
    {
        var result = await adminPortalService.GetDashboardAsync(days, cancellationToken);
        return Ok(result);
    }
}
