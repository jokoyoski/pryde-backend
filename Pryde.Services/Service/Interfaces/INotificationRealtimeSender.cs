using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface INotificationRealtimeSender
{
    Task SendAsync(
        Guid userId,
        NotificationResponseDto notification,
        CancellationToken cancellationToken = default);
}
