using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route(
    "api/v{version:apiVersion}/admin/notifications")]
[Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
public class AdminNotificationsController(
    INotificationService notificationService)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] AdminNotificationsRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await notificationService.AdminGetAllAsync(
            request,
            cancellationToken));
    }

    [HttpGet("{notificationId:guid}")]
    public async Task<IActionResult> Get(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        return Ok(await notificationService.AdminGetByIdAsync(
            notificationId,
            cancellationToken));
    }

    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast(
        [FromBody] AdminBroadcastNotificationRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await notificationService.BroadcastAsync(
            request,
            cancellationToken));
    }
}
