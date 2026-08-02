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
[Route("api/v{version:apiVersion}")]
[Authorize]
public class DriverBookingRequestsController(
    ITripBookingService tripBookingService) : ControllerBase
{
    [HttpGet("trips/{tripId:guid}/booking-requests")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> GetPending(Guid tripId, CancellationToken cancellationToken)
    {
        return Ok(await tripBookingService.GetPendingForTripAsync(tripId, GetUserId(), cancellationToken));
    }

    [HttpGet("driver/booking-requests")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> GetPendingForDriver(
        [FromQuery] DriverBookingRequestsRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await tripBookingService.GetPendingForDriverAsync(
            GetUserId(),
            request,
            cancellationToken));
    }

    [HttpGet("trips/{tripId:guid}/passengers")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> GetPassengers(Guid tripId, CancellationToken cancellationToken)
    {
        return Ok(await tripBookingService.GetConfirmedPassengersAsync(tripId, GetUserId(), cancellationToken));
    }

    [HttpPatch("trip-bookings/{bookingId:guid}/approve")]
    [Authorize(Roles = "Driver")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> Approve(Guid bookingId, CancellationToken cancellationToken)
    {
        return Ok(await tripBookingService.ApproveAsync(bookingId, GetUserId(), cancellationToken));
    }

    [HttpPatch("trip-bookings/{bookingId:guid}/decline")]
    [Authorize(Roles = "Driver")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> Decline(Guid bookingId, CancellationToken cancellationToken)
    {
        return Ok(await tripBookingService.DeclineAsync(bookingId, GetUserId(), cancellationToken));
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
