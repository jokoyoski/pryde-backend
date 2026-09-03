using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class BookingChatRepository(PrydeDbContext context)
    : IBookingChatRepository
{
    public Task<BookingChat?> GetByBookingIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        return context.BookingChats
            .AsNoTracking()
            .FirstOrDefaultAsync(
                chat => chat.BookingId == bookingId,
                cancellationToken);
    }

    public async Task<BookingChat> CreateAsync(
        BookingChat chat,
        CancellationToken cancellationToken = default)
    {
        await context.BookingChats.AddAsync(chat, cancellationToken);
        return chat;
    }

    public async Task<(IReadOnlyList<ChatMessage> Items, int TotalCount)>
        GetMessagesAsync(
            Guid chatId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        var query = context.ChatMessages
            .AsNoTracking()
            .Where(message => message.ChatId == chatId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(message => message.Sender)
                .ThenInclude(sender => sender.Profile)
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public Task<ChatMessage?> GetMessageByClientIdAsync(
        Guid chatId,
        Guid senderId,
        Guid clientMessageId,
        CancellationToken cancellationToken = default)
    {
        return context.ChatMessages
            .AsNoTracking()
            .Include(message => message.Sender)
                .ThenInclude(sender => sender.Profile)
            .FirstOrDefaultAsync(
                message => message.ChatId == chatId &&
                    message.SenderId == senderId &&
                    message.ClientMessageId == clientMessageId,
                cancellationToken);
    }

    public async Task<ChatMessage> CreateMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken = default)
    {
        await context.ChatMessages.AddAsync(message, cancellationToken);
        return message;
    }

    public async Task<(
        IReadOnlyList<AdminBookingChatData> Items,
        int TotalCount)> SearchAsync(
            Guid? bookingId,
            Guid? tripId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        var query = context.BookingChats.AsNoTracking();
        if (bookingId.HasValue)
            query = query.Where(chat => chat.BookingId == bookingId.Value);
        if (tripId.HasValue)
            query = query.Where(chat => chat.Booking.TripId == tripId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(chat => chat.CreatedAt)
            .ThenByDescending(chat => chat.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(chat => new AdminBookingChatData
            {
                ChatId = chat.Id,
                BookingId = chat.BookingId,
                TripId = chat.Booking.TripId,
                DriverId = chat.Booking.Trip.DriverId,
                PassengerId = chat.Booking.PassengerId,
                CreatedAt = chat.CreatedAt,
                DriverEndedAt = chat.Booking.Trip.DriverEndedAt,
                CompletedAt = chat.Booking.Trip.CompletedAt,
                AutoCompletedAt = chat.Booking.Trip.AutoCompletedAt,
                MessageCount = chat.Messages.Count,
                LastMessageAt = chat.Messages
                    .Select(message => (DateTime?)message.SentAt)
                    .Max()
            })
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }
}
