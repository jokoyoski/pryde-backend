using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class NotificationRepository(
    PrydeDbContext context) : INotificationRepository
{
    public async Task<Notification> AddAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        await context.Notifications.AddAsync(
            notification,
            cancellationToken);
        return notification;
    }

    public Task<Notification?> GetByIdAndUserIdAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return context.Notifications.FirstOrDefaultAsync(
            notification =>
                notification.Id == notificationId &&
                notification.UserId == userId,
            cancellationToken);
    }

    public Task<Notification?> GetByDeduplicationKeyAsync(
        string deduplicationKey,
        CancellationToken cancellationToken = default)
    {
        return context.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(
                notification =>
                    notification.DeduplicationKey ==
                    deduplicationKey,
                cancellationToken);
    }

    public async Task<(
        IReadOnlyList<Notification> Items,
        int TotalCount)> GetUserNotificationsAsync(
            Guid userId,
            int pageNumber,
            int pageSize,
            bool? isRead,
            NotificationType? type,
            CancellationToken cancellationToken = default)
    {
        var query = context.Notifications
            .AsNoTracking()
            .Where(notification =>
                notification.UserId == userId);

        if (isRead.HasValue)
        {
            query = query.Where(notification =>
                notification.IsRead == isRead.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(notification =>
                notification.Type == type.Value);
        }

        var totalCount = await query.CountAsync(
            cancellationToken);
        var items = await query
            .OrderByDescending(notification =>
                notification.CreatedAt)
            .ThenByDescending(notification =>
                notification.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return context.Notifications
            .AsNoTracking()
            .CountAsync(
                notification =>
                    notification.UserId == userId &&
                    !notification.IsRead,
                cancellationToken);
    }

    public Task<int> MarkAllAsReadAsync(
        Guid userId,
        DateTime readAt,
        CancellationToken cancellationToken = default)
    {
        return context.Notifications
            .Where(notification =>
                notification.UserId == userId &&
                !notification.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        notification =>
                            notification.IsRead,
                        true)
                    .SetProperty(
                        notification =>
                            notification.ReadAt,
                        readAt)
                    .SetProperty(
                        notification =>
                            notification.UpdatedAt,
                        readAt),
                cancellationToken);
    }

    public Task<bool> ExistsByDeduplicationKeyAsync(
        string deduplicationKey,
        CancellationToken cancellationToken = default)
    {
        return context.Notifications
            .AsNoTracking()
            .AnyAsync(
                notification =>
                    notification.DeduplicationKey ==
                    deduplicationKey,
                cancellationToken);
    }

    public async Task<(
        IReadOnlyList<AdminNotificationRecord> Items,
        int TotalCount)> AdminGetAllAsync(
            int pageNumber,
            int pageSize,
            Guid? userId,
            NotificationType? type,
            bool? isRead,
            DateTime? createdFrom,
            DateTime? createdTo,
            CancellationToken cancellationToken = default)
    {
        var query = AdminQuery();

        if (userId.HasValue)
        {
            query = query.Where(notification =>
                notification.UserId == userId.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(notification =>
                notification.Type == type.Value);
        }

        if (isRead.HasValue)
        {
            query = query.Where(notification =>
                notification.IsRead == isRead.Value);
        }

        if (createdFrom.HasValue)
        {
            var utcFrom =
                createdFrom.Value.ToUniversalTime();
            query = query.Where(notification =>
                notification.CreatedAt >= utcFrom);
        }

        if (createdTo.HasValue)
        {
            var utcTo =
                createdTo.Value.ToUniversalTime();
            query = query.Where(notification =>
                notification.CreatedAt <= utcTo);
        }

        var totalCount = await query.CountAsync(
            cancellationToken);
        var items = await query
            .OrderByDescending(notification =>
                notification.CreatedAt)
            .ThenByDescending(notification =>
                notification.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<AdminNotificationRecord?> AdminGetByIdAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        return AdminQuery().FirstOrDefaultAsync(
            notification =>
                notification.Id == notificationId,
            cancellationToken);
    }

    public void Detach(Notification notification)
    {
        context.Entry(notification).State =
            EntityState.Detached;
    }

    private IQueryable<AdminNotificationRecord>
        AdminQuery()
    {
        return
            from notification in context.Notifications
                .AsNoTracking()
            join user in context.Users.AsNoTracking()
                on notification.UserId equals user.Id
            join profile in context.Profiles.AsNoTracking()
                on user.Id equals profile.UserId
                into profiles
            from profile in profiles.DefaultIfEmpty()
            select new AdminNotificationRecord(
                notification.Id,
                notification.UserId,
                profile == null
                    ? string.Empty
                    : (profile.FirstName + " " +
                       profile.LastName).Trim(),
                user.Email,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.IsRead,
                notification.ReadAt,
                notification.RelatedEntityId,
                notification.RelatedEntityType,
                notification.Action,
                notification.CreatedAt);
    }
}
