using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class BookingChatResponseDto
{
    public Guid ChatId { get; set; }
    public Guid BookingId { get; set; }
    public Guid TripId { get; set; }
    public Guid DriverId { get; set; }
    public Guid PassengerId { get; set; }
    public BookingChatStatus Status { get; set; }
    public bool CanSend { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? RetainUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ChatMessageResponseDto
{
    public Guid MessageId { get; set; }
    public Guid ChatId { get; set; }
    public Guid BookingId { get; set; }
    public Guid ClientMessageId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}

public class AdminBookingChatResponseDto : BookingChatResponseDto
{
    public int MessageCount { get; set; }
    public DateTime? LastMessageAt { get; set; }
}
