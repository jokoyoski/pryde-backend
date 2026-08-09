using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Providers.Paystack;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;

namespace Pryde.Services.Service.Implementation;

public class WalletService : IWalletService
{
    private const string BankName = "Pryde Test Bank";
    private const string Currency = "NGN";
    private readonly IFinancialService? _financialService;
    private readonly IPaystackClient? _paystackClient;
    private readonly PaystackSettings? _paystackSettings;
    private readonly IUnitOfWork unitOfWork;

    public WalletService(
        IUnitOfWork unitOfWork,
        IPaystackClient paystackClient,
        IFinancialService financialService,
        IOptions<PaystackSettings> paystackOptions)
    {
        this.unitOfWork = unitOfWork;
        _paystackClient = paystackClient;
        _financialService = financialService;
        _paystackSettings = paystackOptions.Value;
    }

    public WalletService(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    public async Task<Wallet> CreateWalletForUserAsync(
        User user,
        string accountName,
        CancellationToken cancellationToken = default)
    {
        var existingWallet = await unitOfWork.Wallets.GetByUserIdAsync(
            user.Id,
            cancellationToken);

        if (existingWallet is not null)
        {
            return existingWallet;
        }

        var wallet = new Wallet
        {
            UserId = user.Id,
            Balance = 0,
            EscrowBalance = 0
        };

        await unitOfWork.Wallets.CreateAsync(wallet, cancellationToken);

        await unitOfWork.VirtualAccounts.CreateAsync(
            new VirtualAccount
            {
                WalletId = wallet.Id,
                BankName = BankName,
                AccountName = accountName.Trim(),
                AccountNumber = await GenerateAccountNumberAsync(cancellationToken),
                IsActive = true
            },
            cancellationToken);

        return wallet;
    }

    public async Task<FundVirtualAccountResponseDto> FundVirtualAccountAsync(
        Guid userId,
        FundVirtualAccountRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateFundingRequest(request);

        var accountNumber = request.AccountNumber.Trim();

        var virtualAccount = await unitOfWork.VirtualAccounts.GetByAccountNumberAsync(
            accountNumber,
            cancellationToken)
            ?? throw new NotFoundException(nameof(VirtualAccount), accountNumber);

        if (!virtualAccount.IsActive)
        {
            throw new BadRequestException("Virtual account is inactive.");
        }

        var wallet = virtualAccount.Wallet;
        if (wallet.UserId != userId)
        {
            throw new ForbiddenException("You can fund only your own virtual account.");
        }

        wallet.Balance += request.Amount;
        unitOfWork.Wallets.Update(wallet);

        var transaction = new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = request.Amount,
            Type = WalletTransactionType.Credit,
            Reference = $"FAKE-{Guid.NewGuid():N}"
        };

        await unitOfWork.WalletTransactions.CreateAsync(transaction, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new FundVirtualAccountResponseDto
        {
            AccountNumber = virtualAccount.AccountNumber,
            BankName = virtualAccount.BankName,
            Amount = request.Amount,
            UpdatedBalance = wallet.Balance,
            TransactionId = transaction.Id,
            Reference = transaction.Reference!
        };
    }

    public async Task<WalletResponseDto> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var wallet = await GetWalletAsync(userId, cancellationToken);
        return new WalletResponseDto
        {
            Id = wallet.Id,
            Balance = wallet.Balance,
            EscrowBalance = wallet.EscrowBalance
        };
    }

    public async Task<PagedResponseDto<WalletTransactionResponseDto>> GetTransactionsAsync(
        Guid userId,
        WalletTransactionsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        request ??= new WalletTransactionsRequestDto();
        var wallet = await GetWalletAsync(userId, cancellationToken);
        var result = await unitOfWork.WalletTransactions.GetByWalletIdAsync(
            wallet.Id,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return new PagedResponseDto<WalletTransactionResponseDto>
        {
            Items = result.Items.Select(transaction => new WalletTransactionResponseDto
            {
                Id = transaction.Id,
                Amount = transaction.Amount,
                Type = transaction.Type,
                Status = transaction.Status,
                Description = transaction.Description,
                Reference = transaction.Reference,
                CreatedAt = transaction.CreatedAt
            }).ToList(),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = (int)Math.Ceiling(
                result.TotalCount / (double)request.PageSize)
        };
    }

    public async Task<VirtualAccountResponseDto> GetVirtualAccountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var wallet = await GetWalletAsync(userId, cancellationToken);
        var account = await unitOfWork.VirtualAccounts.GetByWalletIdAsync(wallet.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(VirtualAccount), wallet.Id);

        return new VirtualAccountResponseDto
        {
            Id = account.Id,
            BankName = account.BankName,
            AccountName = account.AccountName,
            AccountNumber = account.AccountNumber,
            IsActive = account.IsActive
        };
    }

    public async Task<PaystackWalletFundingResponseDto>
        VerifyPaystackWalletFundingAsync(
            Guid userId,
            PaystackWalletFundingRequestDto request,
            CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ValidationException("Request cannot be null.");
        }

        var reference = ValidatePaystackReference(request.Reference);
        var user = await unitOfWork.Users.GetByIdAsync(
            userId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);
        var transaction = await PaystackClient.VerifyTransactionAsync(
            reference,
            cancellationToken);

        ValidateSuccessfulPaystackTransaction(
            transaction,
            reference);
        if (!transaction.Customer!.Email.Equals(
                user.Email,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException(
                "The Paystack transaction does not belong to the authenticated user.");
        }

        var amount = ConvertFromKobo(transaction.Amount);
        var result = await FinancialService
            .RecordPaystackWalletFundingAsync(
                userId,
                amount,
                reference,
                cancellationToken);

        return MapPaystackFunding(result.Wallet, result.Transaction);
    }

    public async Task ProcessPaystackWebhookAsync(
        ReadOnlyMemory<byte> payload,
        string? signature,
        CancellationToken cancellationToken = default)
    {
        ValidatePaystackSignature(payload, signature);

        PaystackWebhookEvent webhook;
        try
        {
            webhook = JsonSerializer.Deserialize<PaystackWebhookEvent>(
                payload.Span,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new ValidationException(
                "Invalid Paystack webhook payload.");
        }

        var eventName = webhook.Event.Trim().ToLowerInvariant();
        if (eventName == "charge.success")
        {
            var transaction = webhook.Data
                ?? throw new ValidationException(
                    "Paystack webhook transaction data is required.");
            var reference = ValidatePaystackReference(
                transaction.Reference);
            ValidateSuccessfulPaystackTransaction(
                transaction,
                reference);
            var email = transaction.Customer!.Email.Trim();
            var user = await unitOfWork.Users.GetByEmailAsync(
                email,
                cancellationToken);
            if (user is null)
            {
                return;
            }

            await FinancialService.RecordPaystackWalletFundingAsync(
                user.Id,
                ConvertFromKobo(transaction.Amount),
                reference,
                cancellationToken);
            return;
        }

        if (eventName is
            "transfer.success" or
            "transfer.failed" or
            "transfer.reversed")
        {
            var transfer = webhook.Data
                ?? throw new ValidationException(
                    "Paystack webhook transfer data is required.");
            ValidatePaystackCurrencyAndAmount(transfer);
            var status = eventName switch
            {
                "transfer.success" => "success",
                "transfer.failed" => "failed",
                _ => "reversed"
            };
            await FinancialService.ProcessPaystackTransferStatusAsync(
                ValidatePaystackReference(transfer.Reference),
                transfer.Amount,
                status,
                cancellationToken);
        }
    }

    private async Task<Wallet> GetWalletAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Wallets.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Wallet), userId);
    }

    private IPaystackClient PaystackClient =>
        _paystackClient ?? throw new InvalidOperationException(
            "Paystack client is not available.");

    private IFinancialService FinancialService =>
        _financialService ?? throw new InvalidOperationException(
            "Financial service is not available.");

    private static PaystackWalletFundingResponseDto MapPaystackFunding(
        Wallet wallet,
        WalletTransaction transaction)
    {
        return new PaystackWalletFundingResponseDto
        {
            WalletId = wallet.Id,
            TransactionId = transaction.Id,
            Reference = transaction.Reference!,
            Amount = transaction.Amount,
            NewBalance = wallet.Balance,
            Status = transaction.Status
                ?? WalletTransactionStatus.Successful
        };
    }

    private static void ValidateSuccessfulPaystackTransaction(
        PaystackTransaction transaction,
        string expectedReference)
    {
        if (transaction is null)
        {
            throw new ValidationException(
                "Paystack transaction was not found.");
        }

        if (!transaction.Status.Equals(
                "success",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                "Paystack transaction is not successful.");
        }

        if (!transaction.Reference.Equals(
                expectedReference,
                StringComparison.Ordinal))
        {
            throw new ConflictException(
                "Paystack returned a different transaction reference.");
        }

        ValidatePaystackCurrencyAndAmount(transaction);
        if (transaction.Customer is null ||
            string.IsNullOrWhiteSpace(transaction.Customer.Email))
        {
            throw new ValidationException(
                "Paystack transaction customer is invalid.");
        }
    }

    private static void ValidatePaystackCurrencyAndAmount(
        PaystackTransaction transaction)
    {
        if (transaction.Amount <= 0)
        {
            throw new ValidationException(
                "Paystack transaction amount is invalid.");
        }

        if (!transaction.Currency.Equals(
                Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                "Paystack transaction currency is not supported.");
        }
    }

    private static decimal ConvertFromKobo(long amountInKobo)
    {
        if (amountInKobo <= 0)
        {
            throw new ValidationException(
                "Paystack transaction amount is invalid.");
        }

        return amountInKobo / 100m;
    }

    private static string ValidatePaystackReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ValidationException(
                "Paystack reference is required.");
        }

        var normalized = reference.Trim();
        if (normalized.Length > 100)
        {
            throw new ValidationException(
                "Paystack reference cannot exceed 100 characters.");
        }

        return normalized;
    }

    private void ValidatePaystackSignature(
        ReadOnlyMemory<byte> payload,
        string? signature)
    {
        if (_paystackSettings is null ||
            !_paystackSettings.Enabled ||
            string.IsNullOrWhiteSpace(_paystackSettings.SecretKey))
        {
            throw new ServiceUnavailableException(
                "Paystack is not configured.");
        }

        if (payload.IsEmpty || string.IsNullOrWhiteSpace(signature))
        {
            throw new UnauthorizedException(
                "Invalid Paystack webhook signature.");
        }

        byte[] receivedSignature;
        try
        {
            receivedSignature = Convert.FromHexString(signature.Trim());
        }
        catch (FormatException)
        {
            throw new UnauthorizedException(
                "Invalid Paystack webhook signature.");
        }

        using var hmac = new HMACSHA512(
            System.Text.Encoding.UTF8.GetBytes(
                _paystackSettings.SecretKey));
        var expectedSignature = hmac.ComputeHash(payload.ToArray());
        if (!CryptographicOperations.FixedTimeEquals(
                expectedSignature,
                receivedSignature))
        {
            throw new UnauthorizedException(
                "Invalid Paystack webhook signature.");
        }
    }

    private async Task<string> GenerateAccountNumberAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var accountNumber = RandomNumberGenerator.GetInt32(
                1000000000,
                2000000000)
                .ToString();

            var exists = await unitOfWork.VirtualAccounts.ExistsByAccountNumberAsync(
                accountNumber,
                cancellationToken);

            if (!exists)
            {
                return accountNumber;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique virtual account number.");
    }

    private static void ValidateFundingRequest(FundVirtualAccountRequestDto request)
    {
        if (request is null)
        {
            throw new ValidationException("Request cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(request.AccountNumber))
        {
            throw new ValidationException("Account number is required.");
        }

        if (request.Amount <= 0)
        {
            throw new ValidationException("Amount must be greater than zero.");
        }
    }
}
