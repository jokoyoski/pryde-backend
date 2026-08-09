namespace Pryde.Services.Providers.Paystack;

public interface IPaystackClient
{
    Task<PaystackTransaction> VerifyTransactionAsync(
        string reference,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaystackBank>> GetBanksAsync(
        CancellationToken cancellationToken = default);

    Task<PaystackResolvedAccount> ResolveAccountAsync(
        string bankCode,
        string accountNumber,
        CancellationToken cancellationToken = default);

    Task<PaystackTransferRecipient> CreateTransferRecipientAsync(
        string bankCode,
        string accountNumber,
        string accountName,
        CancellationToken cancellationToken = default);

    Task<PaystackTransferResult> CreateTransferAsync(
        string recipientCode,
        long amountInKobo,
        string reference,
        string reason,
        CancellationToken cancellationToken = default);
}
