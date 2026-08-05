using Microsoft.AspNetCore.SignalR;
using Pryde.Contracts.ResponseModels;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Hubs;

public sealed class SignalRNotificationRealtimeSender(
    IHubContext<NotificationHub, INotificationClient> hubContext)
    : INotificationRealtimeSender
{
    public Task SendAsync(
        Guid userId,
        NotificationResponseDto notification,
        CancellationToken cancellationToken = default)
    {
        return hubContext.Clients
            .User(userId.ToString())
            .ReceiveNotification(notification);
    }
}
