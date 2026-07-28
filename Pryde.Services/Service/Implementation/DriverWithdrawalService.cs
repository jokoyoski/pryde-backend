using Mapster;
using Microsoft.Extensions.Logging;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Providers.Paystack;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class DriverWithdrawalService : IDriverWithdrawalService
{
    private const string Currency = "NGN";
    private const string WithdrawalReason = "Pryde driver withdrawal";
    private readonly IFinancialService _financialService;
    private readonly ILogger<DriverWithdrawalService> _logger;
    private readonly IPaystackClient _paystackClient;
    private readonly IUnitOfWork _unitOfWork;

    public DriverWithdrawalService(
        IFinancialService financialService,
        ILogger<DriverWithdrawalService> logger,
        IPaystackClient paystackClient,
        IUnitOfWork unitOfWork)
    {
        _financialService = financialService;
        _logger = logger;
        _paystackClient = paystackClient;
        _unitOfWork = unitOfWork;
    }

    public async Task<DriverWithdrawalResponseDto> CreateAsync(
        Guid userId,
        CreateDriverWithdrawalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var bankAccount = await _unitOfWork.DriverBankAccounts
            .GetActiveByIdAndUserIdAsync(
                request.DriverBankAccountId,
                userId,
                cancellationToken)
            ?? throw new NotFoundException(
                nameof(DriverBankAccount),
                request.DriverBankAccountId);

        if (string.IsNullOrWhiteSpace(bankAccount.RecipientCode))
        {
            throw new ConflictException(
                "The selected bank account is not ready for withdrawals.");
        }

        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(
            userId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(Wallet), userId);

        if (wallet.Balance < request.Amount)
        {
            throw new ConflictException(
                "The wallet balance is insufficient for this withdrawal.");
        }

        var amountInKobo = ConvertToKobo(request.Amount);
        var providerReference = $"pryde-wd-{Guid.NewGuid():N}";
        var transferResult = await _paystackClient.CreateTransferAsync(
            bankAccount.RecipientCode,
            amountInKobo,
            providerReference,
            WithdrawalReason,
            cancellationToken);
        var transactionStatus = GetTransactionStatus(
            transferResult,
            providerReference);
        var maskedAccountNumber = MaskAccountNumber(
            bankAccount.AccountNumber);

        try
        {
            var walletTransaction = await _financialService
                .RecordDriverWithdrawalAsync(
                    userId,
                    request.Amount,
                    providerReference,
                    bankAccount.BankName,
                    maskedAccountNumber,
                    bankAccount.AccountName,
                    transactionStatus,
                    cancellationToken);

            return walletTransaction.Adapt<DriverWithdrawalResponseDto>();
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "Paystack accepted driver withdrawal {ProviderReference}, but the local transaction failed.",
                providerReference);

            throw;
        }
    }

    public async Task<IReadOnlyList<DriverWithdrawalResponseDto>> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var withdrawals = await _unitOfWork.WalletTransactions
            .GetWithdrawalsByUserIdAsync(
                userId,
                cancellationToken);

        return withdrawals.Adapt<List<DriverWithdrawalResponseDto>>();
    }

    public async Task<DriverWithdrawalResponseDto> GetByIdAsync(
        Guid userId,
        Guid withdrawalId,
        CancellationToken cancellationToken = default)
    {
        if (withdrawalId == Guid.Empty)
        {
            throw new ValidationException(
                "Withdrawal ID is required.");
        }

        var withdrawal = await _unitOfWork.WalletTransactions
            .GetWithdrawalByIdAndUserIdAsync(
                withdrawalId,
                userId,
                cancellationToken)
            ?? throw new NotFoundException(
                nameof(WalletTransaction),
                withdrawalId);

        return withdrawal.Adapt<DriverWithdrawalResponseDto>();
    }

    private static long ConvertToKobo(decimal amount)
    {
        var amountInKobo = amount * 100m;

        if (amountInKobo != decimal.Truncate(amountInKobo))
        {
            throw new ValidationException(
                "Amount cannot contain more than two decimal places.");
        }

        if (amountInKobo > long.MaxValue)
        {
            throw new ValidationException(
                "Amount is too large.");
        }

        return (long)amountInKobo;
    }

    private static WalletTransactionStatus GetTransactionStatus(
        PaystackTransferResult transferResult,
        string providerReference)
    {
        if (!transferResult.Reference.Equals(
                providerReference,
                StringComparison.Ordinal))
        {
            throw new ServiceUnavailableException(
                "Paystack returned a different transfer reference.");
        }

        if (transferResult.Status.Equals(
                "success",
                StringComparison.OrdinalIgnoreCase))
        {
            return WalletTransactionStatus.Successful;
        }

        if (transferResult.Status.Equals(
                "pending",
                StringComparison.OrdinalIgnoreCase))
        {
            return WalletTransactionStatus.Pending;
        }

        if (transferResult.Status.Equals(
                "otp",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ServiceUnavailableException(
                "Paystack transfer OTP must be disabled for withdrawals.");
        }

        throw new ServiceUnavailableException(
            "Paystack did not accept the withdrawal.");
    }

    private static string MaskAccountNumber(string accountNumber)
    {
        if (accountNumber.Length <= 4)
        {
            return accountNumber;
        }

        return new string('*', accountNumber.Length - 4) +
            accountNumber.Substring(accountNumber.Length - 4, 4);
    }

    private static void ValidateRequest(
        CreateDriverWithdrawalRequestDto request)
    {
        if (request == null)
        {
            throw new ValidationException(
                "Request cannot be null.");
        }

        if (request.DriverBankAccountId == Guid.Empty)
        {
            throw new ValidationException(
                "Driver bank account ID is required.");
        }

        if (request.Amount <= 0)
        {
            throw new ValidationException(
                "Amount must be greater than zero.");
        }
    }
}
