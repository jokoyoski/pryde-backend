using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
public class TripRatingsController(
    ITripRatingService tripRatingService) : ControllerBase
{
    [HttpPost("trip-bookings/{bookingId:guid}/ratings")]
    [ProducesResponseType(typeof(TripRatingResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        Guid bookingId,
        [FromBody] TripRatingRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await tripRatingService.CreateAsync(
            bookingId,
            GetUserId(),
            request,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("users/{userId:guid}/rating-summary")]
    public async Task<IActionResult> GetSummary(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return Ok(await tripRatingService.GetSummaryAsync(
            userId,
            cancellationToken));
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(
            ClaimTypes.NameIdentifier)!);
}
