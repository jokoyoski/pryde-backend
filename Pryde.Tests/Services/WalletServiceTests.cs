using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
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
