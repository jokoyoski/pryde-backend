using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IBookingChatRepository
{
    Task<BookingChat?> GetByBookingIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);
    Task<BookingChat> CreateAsync(
        BookingChat chat,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ChatMessage> Items, int TotalCount)>
        GetMessagesAsync(
            Guid chatId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);
    Task<ChatMessage?> GetMessageByClientIdAsync(
        Guid chatId,
        Guid senderId,
        Guid clientMessageId,
        CancellationToken cancellationToken = default);
    Task<ChatMessage> CreateMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AdminBookingChatData> Items, int TotalCount)>
        SearchAsync(
            Guid? bookingId,
            Guid? tripId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);
}

public class AdminBookingChatData
{
    public Guid ChatId { get; set; }
    public Guid BookingId { get; set; }
    public Guid TripId { get; set; }
    public Guid DriverId { get; set; }
    public Guid PassengerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DriverEndedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? AutoCompletedAt { get; set; }
    public int MessageCount { get; set; }
    public DateTime? LastMessageAt { get; set; }
}
