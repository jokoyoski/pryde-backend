namespace Pryde.Contracts.RequestModels;

public class BookingChatMessagesRequestDto : PaginationRequestDto
{
}

public class SendChatMessageRequestDto
{
    public Guid ClientMessageId { get; set; }
    public string MessageText { get; set; } = string.Empty;
}

public class AdminBookingChatsRequestDto : PaginationRequestDto
{
    public Guid? BookingId { get; set; }
    public Guid? TripId { get; set; }
}
