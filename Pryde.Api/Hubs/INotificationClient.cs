using Pryde.Contracts.ResponseModels;

namespace Pryde.Api.Hubs;

public interface INotificationClient
{
    Task ReceiveNotification(
        NotificationResponseDto notification);
}
