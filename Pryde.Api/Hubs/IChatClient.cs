using Pryde.Contracts.ResponseModels;

namespace Pryde.Api.Hubs;

public interface IChatClient
{
    Task ReceiveChatMessage(ChatMessageResponseDto message);
}
