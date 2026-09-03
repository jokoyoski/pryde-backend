using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IBookingChatService
{
    Task<BookingChatResponseDto> GetAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<PagedResponseDto<ChatMessageResponseDto>> GetMessagesAsync(
        Guid bookingId,
        Guid userId,
        BookingChatMessagesRequestDto request,
        CancellationToken cancellationToken = default);
    Task<ChatMessageResponseDto> SendAsync(
        Guid bookingId,
        Guid userId,
        bool isAdmin,
        SendChatMessageRequestDto request,
        CancellationToken cancellationToken = default);
    Task<PagedResponseDto<AdminBookingChatResponseDto>> AdminSearchAsync(
        AdminBookingChatsRequestDto request,
        CancellationToken cancellationToken = default);
    Task<PagedResponseDto<ChatMessageResponseDto>> AdminGetMessagesAsync(
        Guid bookingId,
        BookingChatMessagesRequestDto request,
        CancellationToken cancellationToken = default);
}
