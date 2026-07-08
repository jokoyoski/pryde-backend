using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminController(
    IKycService kycService,
    IUserService userService) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers(
        CancellationToken cancellationToken)
    {
        var users = await userService.GetAllAsync(cancellationToken);
        return Ok(users);
    }

    // KYC approve/reject actions will be added here.
}