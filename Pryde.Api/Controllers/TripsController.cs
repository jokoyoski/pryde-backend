using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Services.Service.Interface;
using Pryde.Api.Authorization;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/trips")]
public class TripsController(ITripService tripService) : ControllerBase
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

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search(
        [FromQuery] SearchTripsRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await tripService.SearchAsync(request, cancellationToken));
    }

    [HttpGet("{tripId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid tripId, CancellationToken cancellationToken)
    {
        return Ok(await tripService.GetByIdAsync(tripId, cancellationToken));
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

    [HttpPost("{tripId:guid}/pickup-confirmation")]
    [Authorize(Roles = "Passenger", Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> ConfirmPickup(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return Ok(await tripService.ConfirmPickupAsync(
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

    [HttpPost("{tripId:guid}/dropoff-confirmation")]
    [Authorize(Roles = "Passenger", Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> ConfirmDropoff(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return Ok(await tripService.ConfirmDropoffAsync(
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
