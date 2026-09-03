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

public class BookingChatService : IBookingChatService
{
    public const int MaximumMessageLength = 2000;
    private const string BookingChatIndexName =
        "IX_BookingChats_BookingId";
    private const string ClientMessageIndexName =
        "IX_ChatMessages_ChatId_SenderId_ClientMessageId";
    private static readonly TimeSpan RetentionPeriod =
        TimeSpan.FromDays(30);
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChatRealtimeSender _realtimeSender;
    private readonly ILogger<BookingChatService> _logger;

    public BookingChatService(
        IUnitOfWork unitOfWork,
        IChatRealtimeSender realtimeSender,
        ILogger<BookingChatService> logger)
    {
        _unitOfWork = unitOfWork;
        _realtimeSender = realtimeSender;
        _logger = logger;
    }

    public BookingChatService(IUnitOfWork unitOfWork)
        : this(
            unitOfWork,
            NullChatRealtimeSender.Instance,
            NullLogger<BookingChatService>.Instance)
    {
    }

    public async Task<BookingChatResponseDto> GetAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var booking = await GetBookingAsync(bookingId, cancellationToken);
        EnsureParticipant(booking, userId);
        EnsureApproved(booking);
        var chat = await EnsureChatAsync(booking, cancellationToken);
        return MapChat(chat, booking, canSend: !IsClosed(booking.Trip));
    }

    public async Task<PagedResponseDto<ChatMessageResponseDto>>
        GetMessagesAsync(
            Guid bookingId,
            Guid userId,
            BookingChatMessagesRequestDto request,
            CancellationToken cancellationToken = default)
    {
        var booking = await GetBookingAsync(bookingId, cancellationToken);
        EnsureParticipant(booking, userId);
        EnsureApproved(booking);
        var chat = await EnsureChatAsync(booking, cancellationToken);
        return await GetMessagesPageAsync(
            chat,
            request,
            cancellationToken);
    }

    public async Task<ChatMessageResponseDto> SendAsync(
        Guid bookingId,
        Guid userId,
        bool isAdmin,
        SendChatMessageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (isAdmin)
            throw new ForbiddenException(
                "Administrators have read-only access to booking chats.");
        ValidateSendRequest(request);

        var booking = await GetBookingAsync(bookingId, cancellationToken);
        EnsureParticipant(booking, userId);
        EnsureApproved(booking);
        if (IsClosed(booking.Trip))
            throw new ConflictException(
                "Chat is closed because the driver has ended the trip.");

        var chat = await EnsureChatAsync(booking, cancellationToken);
        var existing = await _unitOfWork.BookingChats
            .GetMessageByClientIdAsync(
                chat.Id,
                userId,
                request.ClientMessageId,
                cancellationToken);
        if (existing is not null) return MapMessage(existing, bookingId);

        var profile = await _unitOfWork.Profiles.GetByUserIdAsync(
            userId,
            cancellationToken);
        var message = new ChatMessage
        {
            ChatId = chat.Id,
            SenderId = userId,
            ClientMessageId = request.ClientMessageId,
            MessageText = request.MessageText.Trim(),
            SentAt = DateTime.UtcNow
        };
        await _unitOfWork.BookingChats.CreateMessageAsync(
            message,
            cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception, ClientMessageIndexName))
        {
            _unitOfWork.ClearTracking();
            existing = await _unitOfWork.BookingChats
                .GetMessageByClientIdAsync(
                    chat.Id,
                    userId,
                    request.ClientMessageId,
                    cancellationToken);
            if (existing is not null) return MapMessage(existing, bookingId);
            throw;
        }

        var senderName = profile is null
            ? string.Empty
            : $"{profile.FirstName} {profile.LastName}".Trim();
        var response = MapMessage(message, bookingId, senderName);
        await TrySendRealtimeAsync(
            booking.Trip.DriverId,
            booking.PassengerId,
            response,
            cancellationToken);
        return response;
    }

    public async Task<PagedResponseDto<AdminBookingChatResponseDto>>
        AdminSearchAsync(
            AdminBookingChatsRequestDto request,
            CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.BookingChats.SearchAsync(
            request.BookingId,
            request.TripId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
        var items = result.Items.Select(MapAdminChat).ToList();
        return Page(items, request, result.TotalCount);
    }

    public async Task<PagedResponseDto<ChatMessageResponseDto>>
        AdminGetMessagesAsync(
            Guid bookingId,
            BookingChatMessagesRequestDto request,
            CancellationToken cancellationToken = default)
    {
        var booking = await GetBookingAsync(bookingId, cancellationToken);
        EnsureApproved(booking);
        var chat = await EnsureChatAsync(booking, cancellationToken);
        return await GetMessagesPageAsync(
            chat,
            request,
            cancellationToken);
    }

    private async Task<PagedResponseDto<ChatMessageResponseDto>>
        GetMessagesPageAsync(
            BookingChat chat,
            BookingChatMessagesRequestDto request,
            CancellationToken cancellationToken)
    {
        var result = await _unitOfWork.BookingChats.GetMessagesAsync(
            chat.Id,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
        return Page(
            result.Items.Select(message =>
                MapMessage(message, chat.BookingId)).ToList(),
            request,
            result.TotalCount);
    }

    private async Task<TripBooking> GetBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        return await _unitOfWork.TripBookings.GetByIdWithTripAsync(
            bookingId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(TripBooking), bookingId);
    }

    private async Task<BookingChat> EnsureChatAsync(
        TripBooking booking,
        CancellationToken cancellationToken)
    {
        var existing = await _unitOfWork.BookingChats.GetByBookingIdAsync(
            booking.Id,
            cancellationToken);
        if (existing is not null) return existing;

        var chat = new BookingChat { BookingId = booking.Id };
        await _unitOfWork.BookingChats.CreateAsync(chat, cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return chat;
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception, BookingChatIndexName))
        {
            _unitOfWork.ClearTracking();
            var concurrentChat = await _unitOfWork.BookingChats
                .GetByBookingIdAsync(
                booking.Id,
                cancellationToken);
            if (concurrentChat is null) throw;
            return concurrentChat;
        }
    }

    private async Task TrySendRealtimeAsync(
        Guid driverId,
        Guid passengerId,
        ChatMessageResponseDto message,
        CancellationToken cancellationToken)
    {
        try
        {
            await _realtimeSender.SendAsync(
                driverId,
                passengerId,
                message,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Real-time chat delivery failed for message {MessageId}.",
                message.MessageId);
        }
    }

    private static void EnsureParticipant(TripBooking booking, Guid userId)
    {
        if (booking.PassengerId != userId &&
            booking.Trip.DriverId != userId)
        {
            throw new ForbiddenException(
                "Only the booking passenger and trip driver can access this chat.");
        }
    }

    private static void EnsureApproved(TripBooking booking)
    {
        if (!booking.ApprovedAt.HasValue)
            throw new ConflictException(
                "Chat is available only after the booking is approved.");
    }

    private static void ValidateSendRequest(SendChatMessageRequestDto request)
    {
        if (request.ClientMessageId == Guid.Empty)
            throw new ValidationException("Client message ID is required.");
        if (string.IsNullOrWhiteSpace(request.MessageText))
            throw new ValidationException("Message text is required.");
        if (request.MessageText.Trim().Length > MaximumMessageLength)
            throw new ValidationException(
                $"Message text cannot exceed {MaximumMessageLength} characters.");
    }

    private static bool IsClosed(Trip trip)
    {
        return trip.DriverEndedAt.HasValue ||
            trip.Status is TripStatus.DriverEnded or
                TripStatus.DropoffConfirmationPending or
                TripStatus.Completed;
    }

    private static BookingChatResponseDto MapChat(
        BookingChat chat,
        TripBooking booking,
        bool canSend)
    {
        var closedAt = ResolveClosedAt(booking.Trip);
        return new BookingChatResponseDto
        {
            ChatId = chat.Id,
            BookingId = booking.Id,
            TripId = booking.TripId,
            DriverId = booking.Trip.DriverId,
            PassengerId = booking.PassengerId,
            Status = closedAt.HasValue
                ? BookingChatStatus.Closed
                : BookingChatStatus.Open,
            CanSend = canSend,
            ClosedAt = closedAt,
            RetainUntil = ResolveRetainUntil(booking.Trip),
            CreatedAt = chat.CreatedAt
        };
    }

    private static AdminBookingChatResponseDto MapAdminChat(
        AdminBookingChatData chat)
    {
        var closedAt = chat.DriverEndedAt ??
            chat.CompletedAt ??
            chat.AutoCompletedAt;
        return new AdminBookingChatResponseDto
        {
            ChatId = chat.ChatId,
            BookingId = chat.BookingId,
            TripId = chat.TripId,
            DriverId = chat.DriverId,
            PassengerId = chat.PassengerId,
            Status = closedAt.HasValue
                ? BookingChatStatus.Closed
                : BookingChatStatus.Open,
            CanSend = false,
            ClosedAt = closedAt,
            RetainUntil = (chat.CompletedAt ?? chat.AutoCompletedAt)?
                .Add(RetentionPeriod),
            CreatedAt = chat.CreatedAt,
            MessageCount = chat.MessageCount,
            LastMessageAt = chat.LastMessageAt
        };
    }

    private static ChatMessageResponseDto MapMessage(
        ChatMessage message,
        Guid bookingId,
        string? senderName = null)
    {
        var profile = message.Sender?.Profile;
        return new ChatMessageResponseDto
        {
            MessageId = message.Id,
            ChatId = message.ChatId,
            BookingId = bookingId,
            ClientMessageId = message.ClientMessageId,
            SenderId = message.SenderId,
            SenderName = senderName ?? (profile is null
                ? string.Empty
                : $"{profile.FirstName} {profile.LastName}".Trim()),
            MessageText = message.MessageText,
            SentAt = message.SentAt
        };
    }

    private static DateTime? ResolveClosedAt(Trip trip)
    {
        return trip.DriverEndedAt ?? trip.CompletedAt ?? trip.AutoCompletedAt;
    }

    private static DateTime? ResolveRetainUntil(Trip trip)
    {
        return (trip.CompletedAt ?? trip.AutoCompletedAt)?.Add(RetentionPeriod);
    }

    private static bool IsUniqueViolation(
        DbUpdateException exception,
        string indexName)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        } postgresException && postgresException.ConstraintName == indexName;
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

    private sealed class NullChatRealtimeSender : IChatRealtimeSender
    {
        public static NullChatRealtimeSender Instance { get; } = new();

        public Task SendAsync(
            Guid driverId,
            Guid passengerId,
            ChatMessageResponseDto message,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
