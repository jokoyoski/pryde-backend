using Microsoft.AspNetCore.SignalR;
using Pryde.Contracts.ResponseModels;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Hubs;

public sealed class SignalRChatRealtimeSender(
    IHubContext<ChatHub, IChatClient> hubContext)
    : IChatRealtimeSender
{
    public Task SendAsync(
        Guid driverId,
        Guid passengerId,
        ChatMessageResponseDto message,
        CancellationToken cancellationToken = default)
    {
        return hubContext.Clients.Users(
                [driverId.ToString(), passengerId.ToString()])
            .ReceiveChatMessage(message);
    }
}
