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
[Authorize(Roles = "Passenger", Policy = AuthorizationPolicies.EmailVerified)]
public class PassengerRecurringTripsController(
    IRecurringTripService recurringTripService) : ControllerBase
{
    [HttpPost("{recurringTripId:guid}/save")]
    public async Task<IActionResult> Save(
        Guid recurringTripId,
        CancellationToken cancellationToken) =>
        Ok(await recurringTripService.SaveAsync(
            recurringTripId, GetUserId(), cancellationToken));

    [HttpGet("saved")]
    public async Task<IActionResult> GetSaved(
        [FromQuery] SavedRecurringTripsRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await recurringTripService.GetSavedAsync(
            GetUserId(), request, cancellationToken));

    [HttpDelete("{recurringTripId:guid}/save")]
    public async Task<IActionResult> RemoveSaved(
        Guid recurringTripId,
        CancellationToken cancellationToken)
    {
        await recurringTripService.RemoveSavedAsync(
            recurringTripId, GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{recurringTripId:guid}/subscriptions")]
    public async Task<IActionResult> Subscribe(
        Guid recurringTripId,
        CancellationToken cancellationToken)
    {
        var response = await recurringTripService.SubscribeAsync(
            recurringTripId, GetUserId(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("subscriptions/mine")]
    public async Task<IActionResult> GetMine(
        CancellationToken cancellationToken) =>
        Ok(await recurringTripService.GetMySubscriptionsAsync(
            GetUserId(), cancellationToken));

    [HttpPatch("{recurringTripId:guid}/subscriptions/cancel")]
    public async Task<IActionResult> Cancel(
        Guid recurringTripId,
        CancellationToken cancellationToken) =>
        Ok(await recurringTripService.CancelSubscriptionAsync(
            recurringTripId, GetUserId(), cancellationToken));

    private Guid GetUserId() => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
