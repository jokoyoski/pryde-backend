using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
public class TripBookingsController(ITripBookingService tripBookingService) : ControllerBase
{
    [HttpPost("trip-bookings")]
    public async Task<IActionResult> Create(
        [FromBody] CreateTripBookingRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await tripBookingService.CreateAsync(GetUserId(), request.TripId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("trip-bookings/mine")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        return Ok(await tripBookingService.GetMineAsync(GetUserId(), cancellationToken));
    }

    [HttpGet("trips/{tripId:guid}/booking-requests")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> GetPending(Guid tripId, CancellationToken cancellationToken)
    {
        return Ok(await tripBookingService.GetPendingForTripAsync(tripId, GetUserId(), cancellationToken));
    }

    [HttpGet("trips/{tripId:guid}/passengers")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> GetPassengers(Guid tripId, CancellationToken cancellationToken)
    {
        return Ok(await tripBookingService.GetConfirmedPassengersAsync(tripId, GetUserId(), cancellationToken));
    }

    [HttpPatch("trip-bookings/{bookingId:guid}/approve")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> Approve(Guid bookingId, CancellationToken cancellationToken)
    {
        return Ok(await tripBookingService.ApproveAsync(bookingId, GetUserId(), cancellationToken));
    }

    [HttpPatch("trip-bookings/{bookingId:guid}/decline")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> Decline(Guid bookingId, CancellationToken cancellationToken)
    {
        return Ok(await tripBookingService.DeclineAsync(bookingId, GetUserId(), cancellationToken));
    }

    [HttpPatch("trip-bookings/{bookingId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid bookingId, CancellationToken cancellationToken)
    {
        return Ok(await tripBookingService.CancelAsync(bookingId, GetUserId(), cancellationToken));
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
