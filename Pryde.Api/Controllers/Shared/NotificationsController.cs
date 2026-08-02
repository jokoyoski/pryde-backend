using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize]
public class NotificationsController(
    INotificationService notificationService)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMine(
        [FromQuery] UserNotificationsRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await notificationService.GetMineAsync(
            GetUserId(),
            request,
            cancellationToken));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        return Ok(await notificationService
            .GetUnreadCountAsync(
                GetUserId(),
                cancellationToken));
    }

    [HttpPatch("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        return Ok(await notificationService.MarkAsReadAsync(
            notificationId,
            GetUserId(),
            cancellationToken));
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead(
        CancellationToken cancellationToken)
    {
        return Ok(await notificationService
            .MarkAllAsReadAsync(
                GetUserId(),
                cancellationToken));
    }

    private Guid GetUserId()
    {
        return Guid.Parse(
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)!);
    }
}
