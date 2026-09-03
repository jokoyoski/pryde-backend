using Pryde.Domain.Common;

namespace Pryde.Domain.Entities;

public class ChatMessage : BaseEntity
{
    public Guid ChatId { get; set; }
    public BookingChat Chat { get; set; } = null!;
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public Guid ClientMessageId { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}
