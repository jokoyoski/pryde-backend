using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class WalletServiceTests
{
    [Fact]
    public async Task UserCanRetrieveWalletAndVirtualAccount()
    {
        var (unitOfWork, userId, wallet, account) = Context();
        var service = new WalletService(unitOfWork);

        var walletResult = await service.GetMineAsync(userId);
        var accountResult = await service.GetVirtualAccountAsync(userId);

        Assert.Equal(wallet.Id, walletResult.Id);
        Assert.Equal(account.AccountNumber, accountResult.AccountNumber);
    }

    [Fact]
    public async Task DevelopmentFundingUpdatesOwnBalanceAndCreatesOneTransaction()
    {
        var (unitOfWork, userId, wallet, account) = Context();
        var service = new WalletService(unitOfWork);

        var result = await service.FundVirtualAccountAsync(userId, new FundVirtualAccountRequestDto
        {
            AccountNumber = account.AccountNumber,
            Amount = 2500m
        });

        Assert.Equal(2500m, wallet.Balance);
        Assert.Equal(2500m, result.UpdatedBalance);
        Assert.Single(unitOfWork.WalletTransactionRepository.Items);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task UserCannotFundAnotherUsersWallet()
    {
        var (unitOfWork, _, _, account) = Context();
        var service = new WalletService(unitOfWork);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.FundVirtualAccountAsync(
            Guid.NewGuid(), new FundVirtualAccountRequestDto { AccountNumber = account.AccountNumber, Amount = 1m }));
    }

    [Fact]
    public async Task TransactionHistoryIsPagedAndUserScoped()
    {
        var (unitOfWork, userId, wallet, _) = Context();
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

    private static (TestUnitOfWork UnitOfWork, Guid UserId, Wallet Wallet, VirtualAccount Account) Context()
    {
        var unitOfWork = new TestUnitOfWork();
        var userId = Guid.NewGuid();
        var wallet = new Wallet { Id = Guid.NewGuid(), UserId = userId };
        var account = new VirtualAccount
        {
            Id = Guid.NewGuid(), WalletId = wallet.Id, Wallet = wallet,
            AccountNumber = "1000000001", AccountName = "Test User", BankName = "Pryde Test Bank", IsActive = true
        };
        unitOfWork.WalletRepository.Items.Add(wallet);
        unitOfWork.VirtualAccountRepository.Items.Add(account);
        return (unitOfWork, userId, wallet, account);
    }
}
