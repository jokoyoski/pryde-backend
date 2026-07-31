using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class NotificationResponseDto
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? Action { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationCountResponseDto
{
    public int Count { get; set; }
}

public class AdminNotificationResponseDto
    : NotificationResponseDto
{
    public Guid UserId { get; set; }
    public string RecipientName { get; set; } =
        string.Empty;
    public string RecipientEmail { get; set; } =
        string.Empty;
}
