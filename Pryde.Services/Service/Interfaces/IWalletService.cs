using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Entities;

namespace Pryde.Services.Service.Interface;

public interface IWalletService
{
    Task<Wallet> CreateWalletForUserAsync(
        User user,
        string accountName,
        CancellationToken cancellationToken = default);

    Task<WalletResponseDto> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PagedResponseDto<WalletTransactionResponseDto>> GetTransactionsAsync(
        Guid userId,
        WalletTransactionsRequestDto request,
        CancellationToken cancellationToken = default);

    Task<PaystackWalletFundingResponseDto>
        VerifyPaystackWalletFundingAsync(
            Guid userId,
            PaystackWalletFundingRequestDto request,
            CancellationToken cancellationToken = default);

    Task<WalletFundingRequestResponseDto>
        CreateFundingRequestAsync(
            Guid userId,
            WalletFundingRequestDto request,
            CancellationToken cancellationToken = default);

    Task ProcessPaystackWebhookAsync(
        ReadOnlyMemory<byte> payload,
        string? signature,
        CancellationToken cancellationToken = default);
}
