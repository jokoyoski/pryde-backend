using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IChatRealtimeSender
{
    Task SendAsync(
        Guid driverId,
        Guid passengerId,
        ChatMessageResponseDto message,
        CancellationToken cancellationToken = default);
}
