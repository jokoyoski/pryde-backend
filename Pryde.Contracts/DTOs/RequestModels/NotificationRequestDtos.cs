using Pryde.Domain.Enums;

namespace Pryde.Contracts.RequestModels;

public enum NotificationAudience
{
    All = 1,
    Drivers = 2,
    Passengers = 3
}

public class AdminBroadcastNotificationRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationAudience Audience { get; set; }
}

public class UserNotificationsRequestDto
    : PaginationRequestDto
{
    public bool? IsRead { get; set; }
    public NotificationType? Type { get; set; }
}

public class AdminNotificationsRequestDto
    : PaginationRequestDto
{
    public Guid? UserId { get; set; }
    public NotificationType? Type { get; set; }
    public bool? IsRead { get; set; }

    /// <summary>
    /// Inclusive UTC creation timestamp.
    /// </summary>
    public DateTime? CreatedFrom { get; set; }

    /// <summary>
    /// Inclusive UTC creation timestamp.
    /// </summary>
    public DateTime? CreatedTo { get; set; }
}
