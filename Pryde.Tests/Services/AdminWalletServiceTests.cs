using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class AdminWalletServiceTests
{
    [Fact]
    public async Task SuccessfulFundingReturnsUpdatedWalletBalance()
    {
        var context = CreateContext();

        var response = await context.Service.FundWalletAsync(
            ValidRequest(context));

        Assert.Equal(context.Wallet.Id, response.WalletId);
        Assert.Equal(context.User.Id, response.UserId);
        Assert.Equal(500m, response.AmountFunded);
        Assert.Equal(1500m, response.NewBalance);
        Assert.StartsWith(
            "TEST-FUND-",
            response.TransactionReference);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task WalletNotFoundIsRejected()
    {
        var context = CreateContext();
        context.UnitOfWork.WalletRepository.Items.Clear();

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.Service.FundWalletAsync(
                ValidRequest(context)));

        Assert.Equal(0, context.UnitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidAmountIsRejected(decimal amount)
    {
        var context = CreateContext();
        var request = ValidRequest(context);
        request.Amount = amount;

        await Assert.ThrowsAsync<ValidationException>(
            () => context.Service.FundWalletAsync(request));

        Assert.Equal(1000m, context.Wallet.Balance);
        Assert.Equal(0, context.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task UserNotFoundIsRejected()
    {
        var context = CreateContext();
        context.UnitOfWork.UserRepository.Items.Clear();

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.Service.FundWalletAsync(
                ValidRequest(context)));

        Assert.Equal(1000m, context.Wallet.Balance);
    }

    [Fact]
    public async Task FundingCreatesSuccessfulSystemWalletTransaction()
    {
        var context = CreateContext();

        await context.Service.FundWalletAsync(
            ValidRequest(context));

        var transaction = Assert.Single(
            context.UnitOfWork.WalletTransactionRepository.Items);
        Assert.Equal(WalletTransactionType.Credit, transaction.Type);
        Assert.Equal(
            WalletTransactionStatus.Successful,
            transaction.Status);
        Assert.Equal("System", transaction.Provider);
        Assert.Equal("NGN", transaction.Currency);
        Assert.Equal(
            "Manual wallet funding for testing",
            transaction.Description);
        Assert.StartsWith("TEST-FUND-", transaction.Reference);
    }

    [Fact]
    public async Task FundingCreatesBalancedLedgerEntries()
    {
        var context = CreateContext();

        await context.Service.FundWalletAsync(
            ValidRequest(context));

        var ledgerTransaction = Assert.Single(
            context.UnitOfWork.LedgerRepository.Transactions);
        var debitTotal = ledgerTransaction.Entries
            .Where(entry =>
                entry.EntryType == LedgerEntryType.Debit)
            .Sum(entry => entry.Amount);
        var creditTotal = ledgerTransaction.Entries
            .Where(entry =>
                entry.EntryType == LedgerEntryType.Credit)
            .Sum(entry => entry.Amount);

        Assert.Equal(debitTotal, creditTotal);
        Assert.Equal(
            LedgerTransactionType.TestWalletFunding,
            ledgerTransaction.TransactionType);
        Assert.Contains(
            context.UnitOfWork.LedgerRepository.Accounts,
            account =>
                account.AccountType ==
                LedgerAccountType.SystemFunding);
    }

    [Fact]
    public async Task FundingUpdatesBalanceByExactAmount()
    {
        var context = CreateContext();
        var request = ValidRequest(context);
        request.Amount = 275.50m;

        await context.Service.FundWalletAsync(request);

        Assert.Equal(1275.50m, context.Wallet.Balance);
    }

    [Fact]
    public async Task OptionalDescriptionIsStoredWhenProvided()
    {
        var context = CreateContext();
        var request = ValidRequest(context);
        request.Description = "QA withdrawal setup";

        await context.Service.FundWalletAsync(request);

        var transaction = Assert.Single(
            context.UnitOfWork.WalletTransactionRepository.Items);
        Assert.Equal(
            "QA withdrawal setup",
            transaction.Description);
    }

    private static AdminWalletTestContext CreateContext()
    {
        var unitOfWork = new TestUnitOfWork();
        var user = new User
        {
            Email = "wallet-funding@test.local",
            PhoneNumber = "08000000000"
        };
        var wallet = new Wallet
        {
            UserId = user.Id,
            User = user,
            Balance = 1000m
        };
        unitOfWork.UserRepository.Items.Add(user);
        unitOfWork.WalletRepository.Items.Add(wallet);
        var financialService = new FinancialService(unitOfWork);
        var service = new AdminWalletService(
            financialService,
            unitOfWork);

        return new AdminWalletTestContext(
            unitOfWork,
            user,
            wallet,
            service);
    }

    private static AdminFundWalletRequest ValidRequest(
        AdminWalletTestContext context)
    {
        return new AdminFundWalletRequest
        {
            UserId = context.User.Id,
            Amount = 500m
        };
    }

    private sealed class AdminWalletTestContext
    {
        public AdminWalletTestContext(
            TestUnitOfWork unitOfWork,
            User user,
            Wallet wallet,
            AdminWalletService service)
        {
            UnitOfWork = unitOfWork;
            User = user;
            Wallet = wallet;
            Service = service;
        }

        public TestUnitOfWork UnitOfWork { get; }
        public User User { get; }
        public Wallet Wallet { get; }
        public AdminWalletService Service { get; }
    }
}
