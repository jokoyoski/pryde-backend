using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;

namespace Pryde.Services.Service.Interface;

public interface IFinancialService
{
    Task<EscrowResponseDto> HoldBookingPaymentAsync(Guid passengerId, Guid bookingId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<bool> ExpireUnpaidApprovedBookingAsync(
        Guid bookingId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
    Task RefundBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task RefundTripAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task CompleteTripAsync(Guid tripId, Guid driverId, CancellationToken cancellationToken = default);
    Task AutoCompleteTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
    Task<FinancialSummaryResponseDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<PagedResponseDto<EscrowResponseDto>> GetEscrowsAsync(AdminEscrowsRequestDto request, CancellationToken cancellationToken = default);
    Task<EscrowResponseDto> GetEscrowAsync(Guid escrowId, CancellationToken cancellationToken = default);
    Task<PagedResponseDto<LedgerTransactionResponseDto>> GetTransactionsAsync(AdminLedgerTransactionsRequestDto request, CancellationToken cancellationToken = default);
    Task<LedgerTransactionDetailResponseDto> GetTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RevenueSummaryItemResponseDto>> GetRevenueSummaryAsync(int days, CancellationToken cancellationToken = default);
    Task<WalletTransaction> RecordDriverWithdrawalAsync(
        Guid userId,
        decimal amount,
        string providerReference,
        string bankName,
        string maskedAccountNumber,
        string accountName,
        WalletTransactionStatus status,
        CancellationToken cancellationToken = default);
    Task<(Wallet Wallet, WalletTransaction Transaction)>
        RecordTestWalletFundingAsync(
            Guid userId,
            decimal amount,
            string description,
            CancellationToken cancellationToken = default);
}
