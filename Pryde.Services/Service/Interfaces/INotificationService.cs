using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Enums;

namespace Pryde.Services.Service.Interface;

public interface INotificationService
{
    Task<NotificationResponseDto> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default);
    Task<NotificationResponseDto?> TryCreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default);
    Task<PagedResponseDto<NotificationResponseDto>>
        GetMineAsync(
            Guid userId,
            UserNotificationsRequestDto request,
            CancellationToken cancellationToken = default);
    Task<NotificationCountResponseDto>
        GetUnreadCountAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
    Task<NotificationResponseDto> MarkAsReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<NotificationCountResponseDto> MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<PagedResponseDto<AdminNotificationResponseDto>>
        AdminGetAllAsync(
            AdminNotificationsRequestDto request,
            CancellationToken cancellationToken = default);
    Task<AdminNotificationResponseDto> AdminGetByIdAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default);
}

public sealed class CreateNotificationRequest
{
    public Guid UserId { get; init; }
    public NotificationType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Guid? RelatedEntityId { get; init; }
    public string? RelatedEntityType { get; init; }
    public string? Action { get; init; }
    public string? DeduplicationKey { get; init; }
}
