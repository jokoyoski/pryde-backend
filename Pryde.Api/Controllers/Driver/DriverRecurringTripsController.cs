using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Api.Controllers.Driver.Authorization;
using Pryde.Contracts.RequestModels;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/recurring-trips")]
[Authorize(Roles = "Driver", Policy = AuthorizationPolicies.EmailVerified)]
public class DriverRecurringTripsController(
    IRecurringTripService recurringTripService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateRecurringTripRequestDto request,
        CancellationToken cancellationToken)
    {
        var response = await recurringTripService.CreateAsync(
            GetUserId(), request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    public async Task<IActionResult> GetMine(
        CancellationToken cancellationToken) =>
        Ok(await recurringTripService.GetMineAsync(
            GetUserId(), cancellationToken));

    [HttpGet("{recurringTripId:guid}")]
    public async Task<IActionResult> Get(
        Guid recurringTripId,
        CancellationToken cancellationToken) =>
        Ok(await recurringTripService.GetOwnedAsync(
            recurringTripId, GetUserId(), cancellationToken));

    [HttpPut("{recurringTripId:guid}")]
    public async Task<IActionResult> Update(
        Guid recurringTripId,
        [FromBody] UpdateRecurringTripRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await recurringTripService.UpdateAsync(
            recurringTripId, GetUserId(), request, cancellationToken));

    [HttpPatch("{recurringTripId:guid}/pause")]
    public async Task<IActionResult> Pause(
        Guid recurringTripId,
        CancellationToken cancellationToken) =>
        Ok(await recurringTripService.PauseAsync(
            recurringTripId, GetUserId(), cancellationToken));

    [HttpPatch("{recurringTripId:guid}/resume")]
    public async Task<IActionResult> Resume(
        Guid recurringTripId,
        CancellationToken cancellationToken) =>
        Ok(await recurringTripService.ResumeAsync(
            recurringTripId, GetUserId(), cancellationToken));

    [HttpPatch("{recurringTripId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid recurringTripId,
        CancellationToken cancellationToken) =>
        Ok(await recurringTripService.CancelAsync(
            recurringTripId, GetUserId(), cancellationToken));

    private Guid GetUserId() => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
