using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Api.Controllers.Driver.Authorization;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/trips")]
public class PassengerTripsController(ITripService tripService) : ControllerBase
{
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

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
