using Pryde.Domain.Enums;

namespace Pryde.Contracts.RequestModels;

public class UserNotificationsRequestDto
    : PaginationRequestDto
{
    public bool? IsRead { get; set; }
    public NotificationType? Type { get; set; }
}

public class AdminNotificationsRequestDto
    : PaginationRequestDto
{
    public Guid? UserId { get; set; }
    public NotificationType? Type { get; set; }
    public bool? IsRead { get; set; }

    /// <summary>
    /// Inclusive UTC creation timestamp.
    /// </summary>
    public DateTime? CreatedFrom { get; set; }

    /// <summary>
    /// Inclusive UTC creation timestamp.
    /// </summary>
    public DateTime? CreatedTo { get; set; }
}
