using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Pryde.Api.Hubs;

[Authorize]
public sealed class NotificationHub
    : Hub<INotificationClient>
{
}
