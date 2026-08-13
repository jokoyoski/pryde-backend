using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Providers.Paystack;
using Pryde.Services.Settings;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class WalletServiceTests
{
    [Fact]
    public async Task CreateWalletForUserCreatesOnlyWallet()
    {
        var unitOfWork = new TestUnitOfWork();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "wallet-only@test.local"
        };
        var service = new WalletService(unitOfWork);

        var wallet = await service.CreateWalletForUserAsync(
            user,
            "Wallet Only User");

        Assert.Equal(user.Id, wallet.UserId);
        Assert.Single(unitOfWork.WalletRepository.Items);
        Assert.Null(typeof(Wallet).GetProperty("VirtualAccount"));
        Assert.Null(typeof(IUnitOfWork).GetProperty("VirtualAccounts"));
    }

    [Fact]
    public async Task FundingRequestUsesAuthenticatedIdentityAndConvertsNairaToKobo()
    {
        var (unitOfWork, userId, _) = Context();
        var service = PaystackService(
            unitOfWork,
            Transaction("unused", "user@test.local", 10000));

        var result = await service.CreateFundingRequestAsync(
            userId,
            new WalletFundingRequestDto
            {
                Amount = 5000m
            });

        Assert.Equal(500000, result.AmountKobo);
        Assert.Equal("NGN", result.Currency);
        Assert.Equal("user@test.local", result.Email);
        Assert.Equal("pk_test_public", result.PublicKey);
        var fundingRequest = Assert.Single(
            unitOfWork.PaystackWalletFundingRequestRepository.Items);
        Assert.Equal(userId, fundingRequest.UserId);
        Assert.Equal(result.Reference, fundingRequest.Reference);
    }

    [Fact]
    public async Task FundingRequestOwnershipIsRequiredBeforeVerification()
    {
        var (unitOfWork, userId, wallet) = Context();
        AddIntent(unitOfWork, Guid.NewGuid(), "stolen-ref", 10000);
        var service = PaystackService(
            unitOfWork,
            Transaction("stolen-ref", "user@test.local", 10000));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.VerifyPaystackWalletFundingAsync(
                userId,
                new PaystackWalletFundingRequestDto
                {
                    Reference = "stolen-ref"
                }));

        Assert.Equal(0m, wallet.Balance);
    }

    [Fact]
    public async Task PaystackVerificationCreditsTrustedKoboAmountAndPostsLedger()
    {
        var (unitOfWork, userId, wallet) = Context();
        AddIntent(unitOfWork, userId, "pay-ref-1", 250050);
        var service = PaystackService(
            unitOfWork,
            Transaction("pay-ref-1", "user@test.local", 250050));

        var result = await service.VerifyPaystackWalletFundingAsync(
            userId,
            new PaystackWalletFundingRequestDto
            {
                Reference = "pay-ref-1"
            });

        Assert.Equal(2500.50m, result.Amount);
        Assert.Equal(2500.50m, result.NewBalance);
        Assert.Equal(2500.50m, wallet.Balance);
        Assert.Equal("pay-ref-1", result.Reference);
        var walletTransaction = Assert.Single(
            unitOfWork.WalletTransactionRepository.Items);
        Assert.Equal(WalletTransactionType.Credit, walletTransaction.Type);
        Assert.Equal(WalletTransactionStatus.Successful, walletTransaction.Status);
        Assert.Equal("Paystack", walletTransaction.Provider);
        var ledger = Assert.Single(
            unitOfWork.LedgerRepository.Transactions);
        Assert.Equal(
            LedgerTransactionType.PaystackWalletFunding,
            ledger.TransactionType);
        Assert.Equal(2, ledger.Entries.Count);
        Assert.Equal(
            ledger.Entries.Where(entry => entry.EntryType == LedgerEntryType.Debit).Sum(entry => entry.Amount),
            ledger.Entries.Where(entry => entry.EntryType == LedgerEntryType.Credit).Sum(entry => entry.Amount));
        Assert.Equal(
            NotificationType.WalletCredited,
            Assert.Single(unitOfWork.NotificationRepository.Items).Type);
    }

    [Fact]
    public async Task WebhookThenVerifyReturnsExistingFundingWithoutDoubleCredit()
    {
        var (unitOfWork, userId, wallet) = Context();
        var transaction = Transaction(
            "shared-ref",
            "user@test.local",
            100000);
        AddIntent(unitOfWork, userId, "shared-ref", 100000);
        var service = PaystackService(unitOfWork, transaction);
        const string payload =
            "{\"event\":\"charge.success\",\"data\":{" +
            "\"id\":1001,\"domain\":\"test\"," +
            "\"status\":\"success\",\"reference\":\"shared-ref\"," +
            "\"amount\":100000,\"currency\":\"NGN\"," +
            "\"customer\":{\"email\":\"user@test.local\"}}}";

        await service.ProcessPaystackWebhookAsync(
            Encoding.UTF8.GetBytes(payload),
            Signature(payload));
        var result = await service.VerifyPaystackWalletFundingAsync(
            userId,
            new PaystackWalletFundingRequestDto
            {
                Reference = "shared-ref"
            });

        Assert.Equal(1000m, wallet.Balance);
        Assert.Equal(1000m, result.NewBalance);
        Assert.Single(unitOfWork.WalletTransactionRepository.Items);
        Assert.Single(unitOfWork.LedgerRepository.Transactions);
        Assert.Single(unitOfWork.NotificationRepository.Items);
    }

    [Fact]
    public async Task ConcurrentVerificationCreditsFundingRequestOnce()
    {
        var (unitOfWork, userId, wallet) = Context();
        AddIntent(unitOfWork, userId, "concurrent-ref", 100000);
        var service = PaystackService(
            unitOfWork,
            Transaction("concurrent-ref", "user@test.local", 100000));

        await Task.WhenAll(
            service.VerifyPaystackWalletFundingAsync(
                userId,
                new PaystackWalletFundingRequestDto
                {
                    Reference = "concurrent-ref"
                }),
            service.VerifyPaystackWalletFundingAsync(
                userId,
                new PaystackWalletFundingRequestDto
                {
                    Reference = "concurrent-ref"
                }));

        Assert.Equal(1000m, wallet.Balance);
        Assert.Single(unitOfWork.WalletTransactionRepository.Items);
        Assert.Single(unitOfWork.LedgerRepository.Transactions);
    }

    [Fact]
    public async Task ChargeWebhookIgnoresUnknownReference()
    {
        var (unitOfWork, _, wallet) = Context();
        var service = PaystackService(
            unitOfWork,
            Transaction("unknown-ref", "user@test.local", 10000));
        const string payload =
            "{\"event\":\"charge.success\",\"data\":{" +
            "\"id\":1001,\"domain\":\"test\"," +
            "\"status\":\"success\",\"reference\":\"unknown-ref\"," +
            "\"amount\":10000,\"currency\":\"NGN\"," +
            "\"customer\":{\"email\":\"user@test.local\"}}}";

        await service.ProcessPaystackWebhookAsync(
            Encoding.UTF8.GetBytes(payload),
            Signature(payload));

        Assert.Equal(0m, wallet.Balance);
        Assert.Empty(unitOfWork.WalletTransactionRepository.Items);
    }

    [Theory]
    [InlineData(10001, "NGN", "user@test.local", "test")]
    [InlineData(10000, "USD", "user@test.local", "test")]
    [InlineData(10000, "NGN", "other@test.local", "test")]
    [InlineData(10000, "NGN", "user@test.local", "live")]
    public async Task VerificationRejectsIntentMismatches(
        long amount,
        string currency,
        string email,
        string domain)
    {
        var (unitOfWork, userId, wallet) = Context();
        AddIntent(unitOfWork, userId, "mismatch-ref", 10000);
        var transaction = Transaction("mismatch-ref", email, amount);
        transaction.Currency = currency;
        transaction.Domain = domain;
        var service = PaystackService(unitOfWork, transaction);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.VerifyPaystackWalletFundingAsync(
                userId,
                new PaystackWalletFundingRequestDto
                {
                    Reference = "mismatch-ref"
                }));

        Assert.Equal(0m, wallet.Balance);
    }

    [Fact]
    public async Task VerificationRejectsTransactionOwnedByAnotherEmail()
    {
        var (unitOfWork, userId, wallet) = Context();
        AddIntent(unitOfWork, userId, "foreign-ref", 10000);
        var service = PaystackService(
            unitOfWork,
            Transaction("foreign-ref", "other@test.local", 10000));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.VerifyPaystackWalletFundingAsync(
                userId,
                new PaystackWalletFundingRequestDto
                {
                    Reference = "foreign-ref"
                }));

        Assert.Equal(0m, wallet.Balance);
        Assert.Empty(unitOfWork.WalletTransactionRepository.Items);
    }

    [Theory]
    [InlineData("pending", "pay-ref", 10000, "NGN")]
    [InlineData("success", "different-ref", 10000, "NGN")]
    [InlineData("success", "pay-ref", 0, "NGN")]
    [InlineData("success", "pay-ref", 10000, "USD")]
    public async Task VerificationRejectsInvalidProviderTransaction(
        string status,
        string providerReference,
        long amount,
        string currency)
    {
        var (unitOfWork, userId, wallet) = Context();
        AddIntent(unitOfWork, userId, "pay-ref", 10000);
        var transaction = Transaction(
            providerReference,
            "user@test.local",
            amount);
        transaction.Status = status;
        transaction.Currency = currency;
        var service = PaystackService(unitOfWork, transaction);

        var exception = await Record.ExceptionAsync(() =>
            service.VerifyPaystackWalletFundingAsync(
                userId,
                new PaystackWalletFundingRequestDto
                {
                    Reference = "pay-ref"
                }));

        Assert.NotNull(exception);
        Assert.True(
            exception is ConflictException or ValidationException);
        Assert.Equal(0m, wallet.Balance);
        Assert.Empty(unitOfWork.WalletTransactionRepository.Items);
    }

    [Fact]
    public async Task WebhookRejectsInvalidSignature()
    {
        var (unitOfWork, _, _) = Context();
        var service = PaystackService(
            unitOfWork,
            Transaction("pay-ref", "user@test.local", 10000));

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.ProcessPaystackWebhookAsync(
                Encoding.UTF8.GetBytes("{}"),
                "00"));

        Assert.Empty(unitOfWork.WalletTransactionRepository.Items);
    }

    [Fact]
    public async Task TransferFailedWebhookRestoresExistingWithdrawal()
    {
        var (unitOfWork, userId, wallet) = Context();
        wallet.Balance = 500m;
        var withdrawal = new WalletTransaction
        {
            WalletId = wallet.Id,
            Wallet = wallet,
            Amount = 500m,
            Type = WalletTransactionType.Withdrawal,
            Reference = "pryde-wd-hook",
            Status = WalletTransactionStatus.Pending,
            Provider = "Paystack",
            Currency = "NGN"
        };
        unitOfWork.WalletTransactionRepository.Items.Add(withdrawal);
        var service = PaystackService(
            unitOfWork,
            Transaction("unused", "user@test.local", 10000));
        const string payload =
            "{\"event\":\"transfer.failed\",\"data\":{" +
            "\"status\":\"failed\",\"reference\":\"pryde-wd-hook\"," +
            "\"amount\":50000,\"currency\":\"NGN\"}}";

        await service.ProcessPaystackWebhookAsync(
            Encoding.UTF8.GetBytes(payload),
            Signature(payload));

        Assert.Equal(userId, wallet.UserId);
        Assert.Equal(1000m, wallet.Balance);
        Assert.Equal(500m, wallet.WithdrawableBalance);
        Assert.Equal(WalletTransactionStatus.Failed, withdrawal.Status);
        Assert.Equal(
            NotificationType.WithdrawalFailed,
            Assert.Single(unitOfWork.NotificationRepository.Items).Type);
    }

    [Theory]
    [InlineData("transfer.success", WalletTransactionStatus.Successful, 500, 0)]
    [InlineData("transfer.reversed", WalletTransactionStatus.Reversed, 1000, 500)]
    public async Task TransferWebhookAppliesFinalStatusExactlyOnce(
        string eventName,
        WalletTransactionStatus expectedStatus,
        decimal expectedBalance,
        decimal expectedWithdrawableBalance)
    {
        var (unitOfWork, _, wallet) = Context();
        wallet.Balance = 500m;
        var withdrawal = new WalletTransaction
        {
            WalletId = wallet.Id,
            Wallet = wallet,
            Amount = 500m,
            Type = WalletTransactionType.Withdrawal,
            Reference = "pryde-wd-final",
            Status = WalletTransactionStatus.Pending,
            Provider = "Paystack",
            Currency = "NGN"
        };
        unitOfWork.WalletTransactionRepository.Items.Add(withdrawal);
        var service = PaystackService(
            unitOfWork,
            Transaction("unused", "user@test.local", 10000));
        var payload =
            $"{{\"event\":\"{eventName}\",\"data\":{{" +
            "\"status\":\"pending\",\"reference\":\"pryde-wd-final\"," +
            "\"amount\":50000,\"currency\":\"NGN\"}}";

        await service.ProcessPaystackWebhookAsync(
            Encoding.UTF8.GetBytes(payload),
            Signature(payload));
        await service.ProcessPaystackWebhookAsync(
            Encoding.UTF8.GetBytes(payload),
            Signature(payload));

        Assert.Equal(expectedStatus, withdrawal.Status);
        Assert.Equal(expectedBalance, wallet.Balance);
        Assert.Equal(expectedWithdrawableBalance, wallet.WithdrawableBalance);
    }

    [Fact]
    public async Task UserCanRetrieveWallet()
    {
        var (unitOfWork, userId, wallet) = Context();
        var service = new WalletService(unitOfWork);

        var walletResult = await service.GetMineAsync(userId);

        Assert.Equal(wallet.Id, walletResult.Id);
    }

    [Fact]
    public async Task TransactionHistoryIsPagedAndUserScoped()
    {
        var (unitOfWork, userId, wallet) = Context();
        var otherUserId = Guid.NewGuid();
        var otherWallet = new Wallet
        {
            UserId = otherUserId
        };
        unitOfWork.WalletRepository.Items.Add(otherWallet);

        for (var index = 0; index < 5; index++)
        {
            unitOfWork.WalletTransactionRepository.Items.Add(
                Transaction(wallet, index, DateTime.UtcNow.AddMinutes(-index)));
        }

        unitOfWork.WalletTransactionRepository.Items.Add(
            Transaction(otherWallet, 99, DateTime.UtcNow.AddMinutes(1)));
        var service = new WalletService(unitOfWork);

        var result = await service.GetTransactionsAsync(
            userId,
            new WalletTransactionsRequestDto
            {
                PageNumber = 2,
                PageSize = 2
            });

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalPages);
        Assert.All(result.Items, transaction =>
            Assert.NotEqual("other-user", transaction.Reference));
        Assert.All(result.Items, transaction =>
        {
            Assert.Equal(WalletTransactionStatus.Successful, transaction.Status);
            Assert.Equal("Driver earning", transaction.Description);
        });
    }

    private static WalletTransaction Transaction(
        Wallet wallet,
        int index,
        DateTime createdAt)
    {
        return new WalletTransaction
        {
            WalletId = wallet.Id,
            Wallet = wallet,
            Amount = 100m + index,
            Type = WalletTransactionType.EscrowRelease,
            Status = WalletTransactionStatus.Successful,
            Description = "Driver earning",
            Reference = index == 99 ? "other-user" : $"earning-{index}",
            CreatedAt = createdAt
        };
    }

    private static (TestUnitOfWork UnitOfWork, Guid UserId, Wallet Wallet) Context()
    {
        var unitOfWork = new TestUnitOfWork();
        var userId = Guid.NewGuid();
        var wallet = new Wallet { Id = Guid.NewGuid(), UserId = userId };
        unitOfWork.WalletRepository.Items.Add(wallet);
        unitOfWork.UserRepository.Items.Add(new User
        {
            Id = userId,
            Email = "user@test.local",
            Status = UserStatus.Active
        });
        return (unitOfWork, userId, wallet);
    }

    private static WalletService PaystackService(
        TestUnitOfWork unitOfWork,
        PaystackTransaction transaction)
    {
        return new WalletService(
            unitOfWork,
            new FakePaystackClient(transaction),
            new FinancialService(unitOfWork),
            Options.Create(new PaystackSettings
            {
                Enabled = true,
                PublicKey = "pk_test_public",
                SecretKey = "test-secret",
                ExpectedDomain = "test"
            }));
    }

    private static PaystackTransaction Transaction(
        string reference,
        string email,
        long amount)
    {
        return new PaystackTransaction
        {
            Id = 1001,
            Domain = "test",
            Status = "success",
            Reference = reference,
            Amount = amount,
            Currency = "NGN",
            Customer = new PaystackCustomer
            {
                Email = email
            }
        };
    }

    private static void AddIntent(
        TestUnitOfWork unitOfWork,
        Guid userId,
        string reference,
        long amountKobo)
    {
        unitOfWork.PaystackWalletFundingRequestRepository.Items.Add(
            new PaystackWalletFundingRequest
            {
                UserId = userId,
                Reference = reference,
                ExpectedAmountKobo = amountKobo,
                Currency = "NGN",
                CustomerEmail = "user@test.local",
                Status = PaystackWalletFundingRequestStatus.Pending
            });
    }

    private static string Signature(string payload)
    {
        using var hmac = new HMACSHA512(
            Encoding.UTF8.GetBytes("test-secret"));
        return Convert.ToHexString(
                hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
    }

    private sealed class FakePaystackClient(
        PaystackTransaction transaction) : IPaystackClient
    {
        public Task<PaystackTransaction> VerifyTransactionAsync(
            string reference,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(transaction);

        public Task<IReadOnlyList<PaystackBank>> GetBanksAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaystackResolvedAccount> ResolveAccountAsync(
            string bankCode,
            string accountNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaystackTransferRecipient> CreateTransferRecipientAsync(
            string bankCode,
            string accountNumber,
            string accountName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaystackTransferResult> CreateTransferAsync(
            string recipientCode,
            long amountInKobo,
            string reference,
            string reason,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
