using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationRealtimeSender _realtimeSender;
    private readonly ILogger<NotificationService> _logger;
    private const int MaximumTitleLength = 150;
    private const int MaximumMessageLength = 1000;
    private const int MaximumRelatedEntityTypeLength = 100;
    private const int MaximumActionLength = 100;
    private const int MaximumDeduplicationKeyLength = 200;
    private const string DeduplicationIndexName =
        "IX_Notifications_DeduplicationKey";

    public NotificationService(
        IUnitOfWork unitOfWork,
        INotificationRealtimeSender realtimeSender,
        ILogger<NotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _realtimeSender = realtimeSender;
        _logger = logger;
    }

    public NotificationService(
        IUnitOfWork unitOfWork,
        ILogger<NotificationService> logger)
        : this(
            unitOfWork,
            NullNotificationRealtimeSender.Instance,
            logger)
    {
    }

    public NotificationService(IUnitOfWork unitOfWork)
        : this(unitOfWork, NullLogger<NotificationService>.Instance)
    {
    }

    public async Task<NotificationResponseDto> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);

        var deduplicationKey = NormalizeOptional(
            request.DeduplicationKey);
        if (deduplicationKey is not null)
        {
            var existing = await _unitOfWork.Notifications
                .GetByDeduplicationKeyAsync(
                    deduplicationKey,
                    cancellationToken);
            if (existing is not null)
            {
                return Map(existing);
            }
        }

        var notification = new Notification
        {
            UserId = request.UserId,
            Type = request.Type,
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            RelatedEntityId = request.RelatedEntityId,
            RelatedEntityType = NormalizeOptional(
                request.RelatedEntityType),
            Action = NormalizeOptional(request.Action),
            DeduplicationKey = deduplicationKey,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Notifications.AddAsync(
            notification,
            cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
            var response = Map(notification);
            await TrySendRealtimeAsync(
                notification.UserId,
                response,
                cancellationToken);
            return response;
        }
        catch (DbUpdateException exception)
            when (deduplicationKey is not null &&
                  IsDeduplicationConflict(exception))
        {
            _unitOfWork.Notifications.Detach(notification);
            var existing = await _unitOfWork.Notifications
                .GetByDeduplicationKeyAsync(
                    deduplicationKey,
                    cancellationToken);
            if (existing is null)
            {
                throw;
            }

            return Map(existing);
        }
    }

    public async Task<NotificationResponseDto?> TryCreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await CreateAsync(request, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Notification creation failed for event {NotificationType} and resource {ResourceId}.",
                request.Type,
                request.RelatedEntityId);
            return null;
        }
    }

    public async Task<
        PagedResponseDto<NotificationResponseDto>>
        GetMineAsync(
            Guid userId,
            UserNotificationsRequestDto request,
            CancellationToken cancellationToken = default)
    {
        request ??= new UserNotificationsRequestDto();
        var result = await _unitOfWork.Notifications
            .GetUserNotificationsAsync(
                userId,
                request.PageNumber,
                request.PageSize,
                request.IsRead,
                request.Type,
                cancellationToken);

        return Page(
            result.Items.Select(Map).ToList(),
            request,
            result.TotalCount);
    }

    public async Task<NotificationCountResponseDto>
        GetUnreadCountAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        return new NotificationCountResponseDto
        {
            Count = await _unitOfWork.Notifications
                .GetUnreadCountAsync(
                    userId,
                    cancellationToken)
        };
    }

    public async Task<NotificationResponseDto>
        MarkAsReadAsync(
            Guid notificationId,
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.Notifications
            .GetByIdAndUserIdAsync(
                notificationId,
                userId,
                cancellationToken)
            ?? throw new NotFoundException(
                nameof(Notification),
                notificationId);

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }

        return Map(notification);
    }

    public async Task<NotificationCountResponseDto>
        MarkAllAsReadAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        var count = await _unitOfWork.Notifications
            .MarkAllAsReadAsync(
                userId,
                DateTime.UtcNow,
                cancellationToken);
        return new NotificationCountResponseDto
        {
            Count = count
        };
    }

    public async Task<
        PagedResponseDto<AdminNotificationResponseDto>>
        AdminGetAllAsync(
            AdminNotificationsRequestDto request,
            CancellationToken cancellationToken = default)
    {
        request ??= new AdminNotificationsRequestDto();
        ValidateDateRange(
            request.CreatedFrom,
            request.CreatedTo);

        var result = await _unitOfWork.Notifications
            .AdminGetAllAsync(
                request.PageNumber,
                request.PageSize,
                request.UserId,
                request.Type,
                request.IsRead,
                request.CreatedFrom,
                request.CreatedTo,
                cancellationToken);

        return Page(
            result.Items.Select(MapAdmin).ToList(),
            request,
            result.TotalCount);
    }

    public async Task<AdminNotificationResponseDto>
        AdminGetByIdAsync(
            Guid notificationId,
            CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.Notifications
            .AdminGetByIdAsync(
                notificationId,
                cancellationToken)
            ?? throw new NotFoundException(
                nameof(Notification),
                notificationId);
        return MapAdmin(notification);
    }

    private static void ValidateCreateRequest(
        CreateNotificationRequest request)
    {
        if (request is null)
        {
            throw new ValidationException(
                "Notification request is required.");
        }

        if (request.UserId == Guid.Empty)
        {
            throw new ValidationException(
                "Notification user ID is required.");
        }

        if (!Enum.IsDefined(request.Type))
        {
            throw new ValidationException(
                "Notification type is invalid.");
        }

        ValidateRequired(
            request.Title,
            "Notification title",
            MaximumTitleLength);
        ValidateRequired(
            request.Message,
            "Notification message",
            MaximumMessageLength);
        ValidateOptional(
            request.RelatedEntityType,
            "Related entity type",
            MaximumRelatedEntityTypeLength);
        ValidateOptional(
            request.Action,
            "Notification action",
            MaximumActionLength);
        ValidateOptional(
            request.DeduplicationKey,
            "Notification deduplication key",
            MaximumDeduplicationKeyLength);
    }

    private static void ValidateRequired(
        string? value,
        string name,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(
                $"{name} is required.");
        }

        if (value.Trim().Length > maximumLength)
        {
            throw new ValidationException(
                $"{name} cannot exceed {maximumLength} characters.");
        }
    }

    private static void ValidateOptional(
        string? value,
        string name,
        int maximumLength)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            value.Trim().Length > maximumLength)
        {
            throw new ValidationException(
                $"{name} cannot exceed {maximumLength} characters.");
        }
    }

    private static void ValidateDateRange(
        DateTime? createdFrom,
        DateTime? createdTo)
    {
        if (createdFrom.HasValue &&
            createdTo.HasValue &&
            createdFrom.Value.ToUniversalTime() >
            createdTo.Value.ToUniversalTime())
        {
            throw new ValidationException(
                "CreatedFrom cannot be later than CreatedTo.");
        }
    }

    private static bool IsDeduplicationConflict(
        Exception exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is PostgresException postgresException &&
                postgresException.SqlState ==
                    PostgresErrorCodes.UniqueViolation &&
                postgresException.ConstraintName ==
                    DeduplicationIndexName)
            {
                return true;
            }
        }

        return false;
    }

    private async Task TrySendRealtimeAsync(
        Guid userId,
        NotificationResponseDto notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await _realtimeSender.SendAsync(
                userId,
                notification,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Real-time notification delivery failed for notification {NotificationId} and user {UserId}.",
                notification.Id,
                userId);
        }
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static NotificationResponseDto Map(
        Notification notification)
    {
        return new NotificationResponseDto
        {
            Id = notification.Id,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            RelatedEntityId =
                notification.RelatedEntityId,
            RelatedEntityType =
                notification.RelatedEntityType,
            Action = notification.Action,
            CreatedAt = notification.CreatedAt
        };
    }

    private static AdminNotificationResponseDto MapAdmin(
        AdminNotificationRecord notification)
    {
        return new AdminNotificationResponseDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            RecipientName = notification.RecipientName,
            RecipientEmail = notification.RecipientEmail,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            RelatedEntityId =
                notification.RelatedEntityId,
            RelatedEntityType =
                notification.RelatedEntityType,
            Action = notification.Action,
            CreatedAt = notification.CreatedAt
        };
    }

    private static PagedResponseDto<T> Page<T>(
        IReadOnlyList<T> items,
        PaginationRequestDto request,
        int totalCount)
    {
        return new PagedResponseDto<T>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(
                totalCount / (double)request.PageSize)
        };
    }

    private sealed class NullNotificationRealtimeSender
        : INotificationRealtimeSender
    {
        public static NullNotificationRealtimeSender Instance { get; } =
            new();

        public Task SendAsync(
            Guid userId,
            NotificationResponseDto notification,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
