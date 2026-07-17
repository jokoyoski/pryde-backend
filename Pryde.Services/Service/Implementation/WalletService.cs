using System.Security.Cryptography;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class WalletService(IUnitOfWork unitOfWork) : IWalletService
{
    private const string BankName = "Pryde Test Bank";

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

    public async Task<IReadOnlyList<WalletTransactionResponseDto>> GetTransactionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var wallet = await GetWalletAsync(userId, cancellationToken);
        var transactions = await unitOfWork.WalletTransactions.GetByWalletIdAsync(wallet.Id, cancellationToken);

        return transactions.Select(transaction => new WalletTransactionResponseDto
        {
            Id = transaction.Id,
            Amount = transaction.Amount,
            Type = transaction.Type,
            Reference = transaction.Reference,
            CreatedAt = transaction.CreatedAt
        }).ToList();
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

    private async Task<Wallet> GetWalletAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Wallets.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Wallet), userId);
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
