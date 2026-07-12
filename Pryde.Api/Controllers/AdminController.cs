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
    IUserService userService,
    IVehicleService vehicleService) : ControllerBase
{

    // USERS//
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers(CancellationToken cancellationToken)
    {
        var users = await userService.GetAllAsync(cancellationToken);
        return Ok(users);
    }

    // KYC VERIFICATION //

    [HttpPost("kyc/{userId:guid}/approve")]
    public async Task<IActionResult> ApproveKyc(Guid userId, CancellationToken cancellationToken)
    {
        var result = await kycService.ApproveAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("kyc/{userId:guid}/reject")]
    public async Task<IActionResult> RejectKyc(
        Guid userId, [FromBody] string reason, CancellationToken cancellationToken)
    {
        var result = await kycService.RejectAsync(userId, reason, cancellationToken);
        return Ok(result);
    }

    // VEHICLE ACTIVATION/DEACTIVATION //

    [HttpPost("vehicles/{id:guid}/activate")]
    public async Task<IActionResult> ActivateVehicle(Guid id, CancellationToken cancellationToken)
    {
        var result = await vehicleService.ActivateAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("vehicles/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateVehicle(Guid id, CancellationToken cancellationToken)
    {
        var result = await vehicleService.DeactivateAsync(id, cancellationToken);
        return Ok(result);
    }
}