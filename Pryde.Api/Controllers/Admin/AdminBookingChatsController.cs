using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/booking-chats")]
[Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
public class AdminBookingChatsController(
    IBookingChatService bookingChatService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(
        typeof(PagedResponseDto<AdminBookingChatResponseDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] AdminBookingChatsRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await bookingChatService.AdminSearchAsync(
            request,
            cancellationToken));
    }

    [HttpGet("{bookingId:guid}/messages")]
    [ProducesResponseType(
        typeof(PagedResponseDto<ChatMessageResponseDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessages(
        Guid bookingId,
        [FromQuery] BookingChatMessagesRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await bookingChatService.AdminGetMessagesAsync(
            bookingId,
            request,
            cancellationToken));
    }
}
