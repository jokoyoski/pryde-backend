using Pryde.Domain.Entities;
using Pryde.Domain.Enums;

namespace Pryde.Persistence.Repository.Interfaces;

public interface INotificationRepository
{
    Task<Notification> AddAsync(
        Notification notification,
        CancellationToken cancellationToken = default);
    Task<Notification?> GetByIdAndUserIdAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<Notification?> GetByDeduplicationKeyAsync(
        string deduplicationKey,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Notification> Items, int TotalCount)>
        GetUserNotificationsAsync(
            Guid userId,
            int pageNumber,
            int pageSize,
            bool? isRead,
            NotificationType? type,
            CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<int> MarkAllAsReadAsync(
        Guid userId,
        DateTime readAt,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByDeduplicationKeyAsync(
        string deduplicationKey,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AdminNotificationRecord> Items,
        int TotalCount)> AdminGetAllAsync(
            int pageNumber,
            int pageSize,
            Guid? userId,
            NotificationType? type,
            bool? isRead,
            DateTime? createdFrom,
            DateTime? createdTo,
            CancellationToken cancellationToken = default);
    Task<AdminNotificationRecord?> AdminGetByIdAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default);
    void Detach(Notification notification);
}

public sealed record AdminNotificationRecord(
    Guid Id,
    Guid UserId,
    string RecipientName,
    string RecipientEmail,
    NotificationType Type,
    string Title,
    string Message,
    bool IsRead,
    DateTime? ReadAt,
    Guid? RelatedEntityId,
    string? RelatedEntityType,
    string? Action,
    DateTime CreatedAt);
