using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Api.Authorization;
using Pryde.Contracts.RequestModels;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/trips")]
public class DriverTripsController(ITripService tripService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Driver", Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTripRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await tripService.CreateAsync(GetUserId(), request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("mine")]
    [Authorize(Roles = "Driver", Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        return Ok(await tripService.GetMineAsync(GetUserId(), cancellationToken));
    }

    [HttpPut("{tripId:guid}")]
    [Authorize(Roles = "Driver", Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> Update(
        Guid tripId,
        [FromBody] UpdateTripRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await tripService.UpdateAsync(tripId, GetUserId(), request, cancellationToken));
    }

    [HttpPatch("{tripId:guid}/cancel")]
    [Authorize(Roles = "Driver", Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> Cancel(Guid tripId, CancellationToken cancellationToken)
    {
        await tripService.CancelAsync(tripId, GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{tripId:guid}/start")]
    [Authorize(Roles = "Driver", Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> Start(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return Ok(await tripService.StartAsync(
            tripId,
            GetUserId(),
            cancellationToken));
    }

    [HttpPost("{tripId:guid}/end")]
    [Authorize(Roles = "Driver", Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> End(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return Ok(await tripService.EndAsync(
            tripId,
            GetUserId(),
            cancellationToken));
    }

    [HttpPatch("{tripId:guid}/complete")]
    [Authorize(Roles = "Driver", Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> Complete(Guid tripId, CancellationToken cancellationToken)
    {
        return Ok(await tripService.CompleteAsync(tripId, GetUserId(), cancellationToken));
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
