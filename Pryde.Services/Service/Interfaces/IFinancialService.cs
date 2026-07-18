using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IFinancialService
{
    Task<EscrowResponseDto> HoldBookingPaymentAsync(Guid passengerId, Guid bookingId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task RefundBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task RefundTripAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task CompleteTripAsync(Guid tripId, Guid driverId, CancellationToken cancellationToken = default);
    Task<FinancialSummaryResponseDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<PagedResponseDto<EscrowResponseDto>> GetEscrowsAsync(AdminEscrowsRequestDto request, CancellationToken cancellationToken = default);
    Task<EscrowResponseDto> GetEscrowAsync(Guid escrowId, CancellationToken cancellationToken = default);
    Task<PagedResponseDto<LedgerTransactionResponseDto>> GetTransactionsAsync(AdminLedgerTransactionsRequestDto request, CancellationToken cancellationToken = default);
    Task<LedgerTransactionDetailResponseDto> GetTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RevenueSummaryItemResponseDto>> GetRevenueSummaryAsync(int days, CancellationToken cancellationToken = default);
}
