using System.Net;
using System.Text;
using System.Text.Json;
using Mapster;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Mapping;
using Pryde.Services.Providers.Paystack;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Settings;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class DriverBankAccountServiceTests
{
    static DriverBankAccountServiceTests()
    {
        new MapsterConfig().Register(
            TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public async Task BankListReturnsProviderBanks()
    {
        var context = CreateDriverContext();

        var response = await context.Service.GetBanksAsync();

        var bank = Assert.Single(response);
        Assert.Equal("Guaranty Trust Bank", bank.Name);
        Assert.Equal("058", bank.Code);
    }

    [Fact]
    public async Task ResolveAccountReturnsAccountName()
    {
        var context = CreateDriverContext();

        var response = await context.Service.ResolveAccountAsync(
            new ResolveBankAccountRequestDto
            {
                BankCode = "058",
                AccountNumber = "0123456789"
            });

        Assert.Equal("058", response.BankCode);
        Assert.Equal("******6789", response.AccountNumber);
        Assert.Equal("Example Account Name", response.AccountName);
    }

    [Fact]
    public async Task DriverCanSaveValidBankAccount()
    {
        var context = CreateDriverContext();

        var response = await CreateAccountAsync(context);

        var storedAccount = Assert.Single(
            context.UnitOfWork.DriverBankAccountRepository.Items);
        Assert.Equal(context.DriverId, storedAccount.UserId);
        Assert.Equal("058", storedAccount.BankCode);
        Assert.Equal("Guaranty Trust Bank", storedAccount.BankName);
        Assert.Equal(response.Id, storedAccount.Id);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task FirstSavedAccountBecomesDefault()
    {
        var context = CreateDriverContext();

        var response = await CreateAccountAsync(context);

        Assert.True(response.IsDefault);
        Assert.True(
            context.UnitOfWork.DriverBankAccountRepository
                .Items[0].IsDefault);
    }

    [Fact]
    public async Task SavedAccountResponseMasksAccountNumber()
    {
        var context = CreateDriverContext();

        var response = await CreateAccountAsync(context);

        Assert.Equal("******6789", response.AccountNumber);
    }

    [Fact]
    public async Task RecipientCodeIsStoredButNotReturned()
    {
        var context = CreateDriverContext();

        var response = await CreateAccountAsync(context);

        var storedAccount = Assert.Single(
            context.UnitOfWork.DriverBankAccountRepository.Items);
        Assert.Equal("RCP_test_recipient", storedAccount.RecipientCode);

        var responseJson = JsonSerializer.Serialize(response);
        Assert.DoesNotContain(
            "recipient",
            responseJson,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateAccountIsRejectedBeforeProviderCalls()
    {
        var context = CreateDriverContext();
        await CreateAccountAsync(context);
        var resolveCallCount = context.PaystackClient.ResolveCallCount;
        var recipientCallCount =
            context.PaystackClient.CreateRecipientCallCount;

        await Assert.ThrowsAsync<ConflictException>(
            async () =>
            {
                await CreateAccountAsync(context);
            });

        Assert.Equal(
            resolveCallCount,
            context.PaystackClient.ResolveCallCount);
        Assert.Equal(
            recipientCallCount,
            context.PaystackClient.CreateRecipientCallCount);
    }

    [Fact]
    public async Task InvalidBankCodeIsRejected()
    {
        var context = CreateDriverContext();

        await Assert.ThrowsAsync<ValidationException>(
            async () =>
            {
                await context.Service.ResolveAccountAsync(
                    new ResolveBankAccountRequestDto
                    {
                        BankCode = "999",
                        AccountNumber = "0123456789"
                    });
            });
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("12345678901")]
    public async Task AccountNumberWithInvalidLengthIsRejected(
        string accountNumber)
    {
        var context = CreateDriverContext();

        await Assert.ThrowsAsync<ValidationException>(
            async () =>
            {
                await context.Service.ResolveAccountAsync(
                    new ResolveBankAccountRequestDto
                    {
                        BankCode = "058",
                        AccountNumber = accountNumber
                    });
            });
    }

    [Fact]
    public async Task AccountNumberContainingLettersIsRejected()
    {
        var context = CreateDriverContext();

        await Assert.ThrowsAsync<ValidationException>(
            async () =>
            {
                await context.Service.ResolveAccountAsync(
                    new ResolveBankAccountRequestDto
                    {
                        BankCode = "058",
                        AccountNumber = "01234A6789"
                    });
            });
    }

    [Fact]
    public async Task DriverCanListOnlyTheirOwnActiveAccounts()
    {
        var context = CreateDriverContext();
        var otherDriverId = Guid.NewGuid();
        var ownDefault = BankAccount(
            context.DriverId,
            "058",
            "0123456789",
            true,
            true);
        var ownInactive = BankAccount(
            context.DriverId,
            "044",
            "1234567890",
            false,
            false);
        var otherAccount = BankAccount(
            otherDriverId,
            "058",
            "0987654321",
            true,
            true);

        context.UnitOfWork.DriverBankAccountRepository.Items.Add(
            ownDefault);
        context.UnitOfWork.DriverBankAccountRepository.Items.Add(
            ownInactive);
        context.UnitOfWork.DriverBankAccountRepository.Items.Add(
            otherAccount);

        var response = await context.Service.GetMineAsync(
            context.DriverId);

        var account = Assert.Single(response);
        Assert.Equal(ownDefault.Id, account.Id);
        Assert.Equal("******6789", account.AccountNumber);
    }

    [Fact]
    public async Task DriverCanSwitchToReplacementBankAccount()
    {
        var context = CreateDriverContext();
        var oldAccount = BankAccount(
            context.DriverId,
            "058",
            "0123456789",
            true,
            true);
        context.UnitOfWork.DriverBankAccountRepository.Items.Add(oldAccount);

        var response = await context.Service.CreateAsync(
            context.DriverId,
            new CreateDriverBankAccountRequestDto
            {
                BankCode = "058",
                AccountNumber = "0987654321"
            });

        Assert.False(oldAccount.IsActive);
        Assert.False(oldAccount.IsDefault);
        Assert.True(response.IsActive);
        Assert.True(response.IsDefault);
        Assert.Equal("******4321", response.AccountNumber);
        Assert.Equal(2, context.UnitOfWork.DriverBankAccountRepository.Items.Count);
    }

    [Fact]
    public async Task SwitchingAccountDoesNotChangeAnotherUsersAccount()
    {
        var context = CreateDriverContext();
        var ownAccount = BankAccount(
            context.DriverId,
            "058",
            "0123456789",
            true,
            true);
        var otherAccount = BankAccount(
            Guid.NewGuid(),
            "058",
            "1111111111",
            true,
            true);
        context.UnitOfWork.DriverBankAccountRepository.Items.Add(ownAccount);
        context.UnitOfWork.DriverBankAccountRepository.Items.Add(otherAccount);

        await context.Service.CreateAsync(
            context.DriverId,
            new CreateDriverBankAccountRequestDto
            {
                BankCode = "058",
                AccountNumber = "0987654321"
            });

        Assert.False(ownAccount.IsActive);
        Assert.True(otherAccount.IsActive);
        Assert.True(otherAccount.IsDefault);
    }

    [Fact]
    public async Task SwitchingAccountPreservesHistoricalWithdrawalDetails()
    {
        var context = CreateDriverContext();
        var oldAccount = BankAccount(
            context.DriverId,
            "058",
            "0123456789",
            true,
            true);
        context.UnitOfWork.DriverBankAccountRepository.Items.Add(oldAccount);
        var withdrawal = new WalletTransaction
        {
            Type = WalletTransactionType.Withdrawal,
            BankName = oldAccount.BankName,
            MaskedAccountNumber = "******6789",
            AccountName = oldAccount.AccountName,
            Reference = "withdrawal-history"
        };
        context.UnitOfWork.WalletTransactionRepository.Items.Add(withdrawal);

        await context.Service.CreateAsync(
            context.DriverId,
            new CreateDriverBankAccountRequestDto
            {
                BankCode = "058",
                AccountNumber = "0987654321"
            });

        Assert.Contains(oldAccount,
            context.UnitOfWork.DriverBankAccountRepository.Items);
        Assert.False(oldAccount.IsActive);
        Assert.Equal("******6789", withdrawal.MaskedAccountNumber);
        Assert.Equal("withdrawal-history", withdrawal.Reference);
    }

    [Fact]
    public async Task PassengerOnlyUserCannotSaveDriverBankAccount()
    {
        var unitOfWork = new TestUnitOfWork();
        var passengerId = Guid.NewGuid();
        unitOfWork.UserRoleRepository.Items.Add(
            new UserRole
            {
                UserId = passengerId,
                Role = new Role
                {
                    Name = RoleNames.Passenger
                }
            });
        var service = new DriverBankAccountService(
            new FakePaystackClient(),
            unitOfWork);

        await Assert.ThrowsAsync<ForbiddenException>(
            async () =>
            {
                await service.CreateAsync(
                    passengerId,
                    ValidCreateRequest());
            });
    }

    [Fact]
    public async Task PaystackFailureReturnsControlledApplicationError()
    {
        var context = CreateDriverContext();
        context.PaystackClient.Failure = new ServiceUnavailableException(
            "Paystack is currently unavailable.");

        await Assert.ThrowsAsync<ServiceUnavailableException>(
            async () =>
            {
                await context.Service.GetBanksAsync();
            });
    }

    [Fact]
    public async Task PaystackDisabledReturnsServiceUnavailable()
    {
        using (var httpClient = new HttpClient(
                   new StubHttpMessageHandler(
                       HttpStatusCode.OK,
                       "{}")))
        {
            var client = new PaystackClient(
                httpClient,
                Options.Create(new PaystackSettings
                {
                    Enabled = false
                }),
                NullLogger<PaystackClient>.Instance);

            await Assert.ThrowsAsync<ServiceUnavailableException>(
                async () =>
                {
                    await client.GetBanksAsync();
                });
        }
    }

    [Fact]
    public async Task PaystackRejectedResponseReturnsControlledError()
    {
        const string responseJson =
            "{\"status\":false,\"message\":\"Invalid bank\"}";

        using (var httpClient = new HttpClient(
                   new StubHttpMessageHandler(
                       HttpStatusCode.BadRequest,
                       responseJson)))
        {
            httpClient.BaseAddress = new Uri(
                "https://api.paystack.co/");

            var client = new PaystackClient(
                httpClient,
                Options.Create(EnabledSettings()),
                NullLogger<PaystackClient>.Instance);

            await Assert.ThrowsAsync<ServiceUnavailableException>(
                async () =>
                {
                    await client.ResolveAccountAsync(
                        "058",
                        "0123456789");
                });
        }
    }

    private static DriverBankAccountTestContext CreateDriverContext()
    {
        var unitOfWork = new TestUnitOfWork();
        var driverId = Guid.NewGuid();
        unitOfWork.UserRoleRepository.Items.Add(
            new UserRole
            {
                UserId = driverId,
                Role = new Role
                {
                    Name = RoleNames.Driver
                }
            });

        var paystackClient = new FakePaystackClient();
        var service = new DriverBankAccountService(
            paystackClient,
            unitOfWork);

        return new DriverBankAccountTestContext(
            unitOfWork,
            driverId,
            paystackClient,
            service);
    }

    private static async Task<
        Pryde.Contracts.ResponseModels.DriverBankAccountResponseDto>
        CreateAccountAsync(
            DriverBankAccountTestContext context)
    {
        return await context.Service.CreateAsync(
            context.DriverId,
            ValidCreateRequest());
    }

    private static CreateDriverBankAccountRequestDto ValidCreateRequest()
    {
        return new CreateDriverBankAccountRequestDto
        {
            BankCode = "058",
            AccountNumber = "0123456789"
        };
    }

    private static DriverBankAccount BankAccount(
        Guid userId,
        string bankCode,
        string accountNumber,
        bool isDefault,
        bool isActive)
    {
        return new DriverBankAccount
        {
            UserId = userId,
            BankCode = bankCode,
            BankName = "Test Bank",
            AccountNumber = accountNumber,
            AccountName = "Test Account",
            RecipientCode = "RCP_test",
            IsDefault = isDefault,
            IsActive = isActive
        };
    }

    private static PaystackSettings EnabledSettings()
    {
        return new PaystackSettings
        {
            Enabled = true,
            BaseUrl = "https://api.paystack.co",
            SecretKey = "test-secret"
        };
    }

    private sealed class DriverBankAccountTestContext
    {
        public DriverBankAccountTestContext(
            TestUnitOfWork unitOfWork,
            Guid driverId,
            FakePaystackClient paystackClient,
            DriverBankAccountService service)
        {
            UnitOfWork = unitOfWork;
            DriverId = driverId;
            PaystackClient = paystackClient;
            Service = service;
        }

        public TestUnitOfWork UnitOfWork { get; }
        public Guid DriverId { get; }
        public FakePaystackClient PaystackClient { get; }
        public DriverBankAccountService Service { get; }
    }

    private sealed class FakePaystackClient : IPaystackClient
    {
        public Exception? Failure { get; set; }
        public int ResolveCallCount { get; private set; }
        public int CreateRecipientCallCount { get; private set; }

        public Task<PaystackTransaction> VerifyTransactionAsync(
            string reference,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<PaystackBank>> GetBanksAsync(
            CancellationToken cancellationToken = default)
        {
            ThrowIfFailed();

            IReadOnlyList<PaystackBank> banks =
                new List<PaystackBank>
            {
                new PaystackBank
                {
                    Name = "Guaranty Trust Bank",
                    Code = "058",
                    Active = true
                }
            };

            return Task.FromResult(banks);
        }

        public Task<PaystackResolvedAccount> ResolveAccountAsync(
            string bankCode,
            string accountNumber,
            CancellationToken cancellationToken = default)
        {
            ThrowIfFailed();
            ResolveCallCount++;

            return Task.FromResult(
                new PaystackResolvedAccount
                {
                    AccountNumber = accountNumber,
                    AccountName = "Example Account Name"
                });
        }

        public Task<PaystackTransferRecipient>
            CreateTransferRecipientAsync(
                string bankCode,
                string accountNumber,
                string accountName,
                CancellationToken cancellationToken = default)
        {
            ThrowIfFailed();
            CreateRecipientCallCount++;

            return Task.FromResult(
                new PaystackTransferRecipient
                {
                    RecipientCode = "RCP_test_recipient"
                });
        }

        public Task<PaystackTransferResult> CreateTransferAsync(
            string recipientCode,
            long amountInKobo,
            string reference,
            string reason,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        private void ThrowIfFailed()
        {
            if (Failure != null)
            {
                throw Failure;
            }
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseJson;

        public StubHttpMessageHandler(
            HttpStatusCode statusCode,
            string responseJson)
        {
            _statusCode = statusCode;
            _responseJson = responseJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(
                    _responseJson,
                    Encoding.UTF8,
                    "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
