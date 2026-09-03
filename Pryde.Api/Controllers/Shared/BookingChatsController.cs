using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Api.Controllers.Driver.Authorization;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/trip-bookings/{bookingId:guid}/chat")]
[Authorize(Roles = RoleNames.Driver + "," + RoleNames.Passenger)]
public class BookingChatsController(
    IBookingChatService bookingChatService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(BookingChatResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        return Ok(await bookingChatService.GetAsync(
            bookingId,
            GetUserId(),
            cancellationToken));
    }

    [HttpGet("messages")]
    [ProducesResponseType(
        typeof(PagedResponseDto<ChatMessageResponseDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessages(
        Guid bookingId,
        [FromQuery] BookingChatMessagesRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await bookingChatService.GetMessagesAsync(
            bookingId,
            GetUserId(),
            request,
            cancellationToken));
    }

    [HttpPost("messages")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    [ProducesResponseType(typeof(ChatMessageResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Send(
        Guid bookingId,
        [FromBody] SendChatMessageRequestDto request,
        CancellationToken cancellationToken)
    {
        var message = await bookingChatService.SendAsync(
            bookingId,
            GetUserId(),
            User.IsInRole(RoleNames.Admin) ||
                User.IsInRole(RoleNames.SuperAdmin),
            request,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, message);
    }

    private Guid GetUserId() => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
