using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Mapster;
using Microsoft.Extensions.Logging.Abstractions;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Mapping;
using Pryde.Services.Notifications.Interface;
using Pryde.Services.Providers.Paystack;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class DriverWithdrawalServiceTests
{
    static DriverWithdrawalServiceTests()
    {
        new MapsterConfig().Register(
            TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public async Task DriverCanCreateValidWithdrawal()
    {
        var context = CreateContext();

        var response = await context.Service.CreateAsync(
            context.DriverId,
            ValidRequest(context));

        Assert.Equal(500m, response.Amount);
        Assert.Equal("NGN", response.Currency);
        Assert.Equal(
            WalletTransactionStatus.Successful,
            response.Status);
        Assert.Equal("******6789", response.MaskedAccountNumber);
        Assert.Equal(1500m, context.Wallet.Balance);
        Assert.Single(
            context.UnitOfWork.WalletTransactionRepository.Items);
        Assert.Equal(2, context.UnitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveAmountIsRejected(decimal amount)
    {
        var context = CreateContext();
        var request = ValidRequest(context);
        request.Amount = amount;

        await Assert.ThrowsAsync<ValidationException>(
            () => context.Service.CreateAsync(
                context.DriverId,
                request));

        Assert.Equal(2000m, context.Wallet.Balance);
        Assert.Equal(0, context.PaystackClient.TransferCallCount);
    }

    [Fact]
    public async Task MissingWalletIsRejectedBeforePaystackCall()
    {
        var context = CreateContext();
        context.UnitOfWork.WalletRepository.Items.Clear();

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.Service.CreateAsync(
                context.DriverId,
                ValidRequest(context)));

        Assert.Equal(0, context.PaystackClient.TransferCallCount);
    }

    [Fact]
    public async Task InsufficientBalanceIsRejectedBeforePaystackCall()
    {
        var context = CreateContext();
        context.Wallet.Balance = 100m;

        await Assert.ThrowsAsync<ConflictException>(
            () => context.Service.CreateAsync(
                context.DriverId,
                ValidRequest(context)));

        Assert.Equal(100m, context.Wallet.Balance);
        Assert.Equal(0, context.PaystackClient.TransferCallCount);
    }

    [Fact]
    public async Task MissingBankAccountIsRejected()
    {
        var context = CreateContext();
        context.UnitOfWork.DriverBankAccountRepository.Items.Clear();

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.Service.CreateAsync(
                context.DriverId,
                ValidRequest(context)));
    }

    [Fact]
    public async Task BankAccountOwnedByAnotherDriverIsRejected()
    {
        var context = CreateContext();
        context.BankAccount.UserId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.Service.CreateAsync(
                context.DriverId,
                ValidRequest(context)));
    }

    [Fact]
    public async Task InactiveBankAccountIsRejected()
    {
        var context = CreateContext();
        context.BankAccount.IsActive = false;

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.Service.CreateAsync(
                context.DriverId,
                ValidRequest(context)));
    }

    [Fact]
    public async Task MissingRecipientCodeIsRejected()
    {
        var context = CreateContext();
        context.BankAccount.RecipientCode = string.Empty;

        await Assert.ThrowsAsync<ConflictException>(
            () => context.Service.CreateAsync(
                context.DriverId,
                ValidRequest(context)));
    }

    [Fact]
    public async Task PaystackFailureDoesNotDebitOrSaveTransaction()
    {
        var context = CreateContext();
        context.PaystackClient.Failure =
            new ServiceUnavailableException(
                "Paystack is unavailable.");

        await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => context.Service.CreateAsync(
                context.DriverId,
                ValidRequest(context)));

        Assert.Equal(2000m, context.Wallet.Balance);
        Assert.Empty(
            context.UnitOfWork.WalletTransactionRepository.Items);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task SuccessfulWithdrawalUsesStoredRecipientAndKoboAmount()
    {
        var context = CreateContext();

        await context.Service.CreateAsync(
            context.DriverId,
            ValidRequest(context));

        Assert.Equal(
            context.BankAccount.RecipientCode,
            context.PaystackClient.RecipientCode);
        Assert.Equal(50000, context.PaystackClient.AmountInKobo);
        Assert.Equal("Pryde driver withdrawal", context.PaystackClient.Reason);
        Assert.StartsWith(
            "pryde-wd-",
            context.PaystackClient.Reference);
    }

    [Fact]
    public async Task SuccessfulWithdrawalCreatesOneDebitAndBalancedLedger()
    {
        var context = CreateContext();

        await context.Service.CreateAsync(
            context.DriverId,
            ValidRequest(context));

        var transaction = Assert.Single(
            context.UnitOfWork.WalletTransactionRepository.Items);
        Assert.Equal(WalletTransactionType.Withdrawal, transaction.Type);
        Assert.Equal("NGN", transaction.Currency);
        Assert.Equal("Paystack", transaction.Provider);
        Assert.Equal(
            context.PaystackClient.Reference,
            transaction.Reference);

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
            LedgerTransactionType.DriverWithdrawal,
            ledgerTransaction.TransactionType);
    }

    [Fact]
    public async Task DriverSeesOnlyOwnWithdrawalsNewestFirst()
    {
        var context = CreateContext();
        var otherWallet = new Wallet
        {
            UserId = Guid.NewGuid()
        };
        context.UnitOfWork.WalletRepository.Items.Add(otherWallet);
        context.UnitOfWork.WalletTransactionRepository.Items.AddRange(
            new WalletTransaction
            {
                WalletId = context.Wallet.Id,
                Type = WalletTransactionType.Withdrawal,
                Status = WalletTransactionStatus.Successful,
                Currency = "NGN",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new WalletTransaction
            {
                WalletId = context.Wallet.Id,
                Type = WalletTransactionType.Withdrawal,
                Status = WalletTransactionStatus.Successful,
                Currency = "NGN",
                CreatedAt = DateTime.UtcNow
            },
            new WalletTransaction
            {
                WalletId = otherWallet.Id,
                Type = WalletTransactionType.Withdrawal,
                Status = WalletTransactionStatus.Successful,
                Currency = "NGN",
                CreatedAt = DateTime.UtcNow.AddDays(1)
            });

        var response = await context.Service.GetMineAsync(
            context.DriverId);

        Assert.Equal(2, response.Count);
        Assert.True(response[0].CreatedAt > response[1].CreatedAt);
    }

    [Fact]
    public async Task DriverCannotRetrieveAnotherDriversWithdrawal()
    {
        var context = CreateContext();
        var otherWallet = new Wallet
        {
            UserId = Guid.NewGuid()
        };
        var otherWithdrawal = new WalletTransaction
        {
            WalletId = otherWallet.Id,
            Type = WalletTransactionType.Withdrawal,
            Status = WalletTransactionStatus.Successful,
            Currency = "NGN"
        };
        context.UnitOfWork.WalletRepository.Items.Add(otherWallet);
        context.UnitOfWork.WalletTransactionRepository.Items.Add(
            otherWithdrawal);

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.Service.GetByIdAsync(
                context.DriverId,
                otherWithdrawal.Id));
    }

    [Fact]
    public async Task RecipientCodeAndRawAccountNumberAreNotReturned()
    {
        var context = CreateContext();

        var response = await context.Service.CreateAsync(
            context.DriverId,
            ValidRequest(context));
        var serializedResponse =
            System.Text.Json.JsonSerializer.Serialize(response);

        Assert.DoesNotContain(
            context.BankAccount.RecipientCode,
            serializedResponse);
        Assert.DoesNotContain(
            context.BankAccount.AccountNumber,
            serializedResponse);
        Assert.Equal("******6789", response.MaskedAccountNumber);
    }

    [Fact]
    public async Task OtpTransferDoesNotDebitWallet()
    {
        var context = CreateContext();
        context.PaystackClient.Status = "otp";

        await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => context.Service.CreateAsync(
                context.DriverId,
                ValidRequest(context)));

        Assert.Equal(2000m, context.Wallet.Balance);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ConcurrentWithdrawalsCannotProduceNegativeBalance()
    {
        var context = CreateContext();
        context.Wallet.Balance = 500m;

        var firstTask = context.Service.CreateAsync(
            context.DriverId,
            ValidRequest(context));
        var secondTask = context.Service.CreateAsync(
            context.DriverId,
            ValidRequest(context));

        var exceptions = new List<Exception>();

        try
        {
            await firstTask;
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }

        try
        {
            await secondTask;
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }

        Assert.Equal(0m, context.Wallet.Balance);
        Assert.Single(
            context.UnitOfWork.WalletTransactionRepository.Items);
        Assert.Single(exceptions);
        Assert.IsType<ValidationException>(exceptions[0]);
        Assert.Equal(1, context.PaystackClient.TransferCallCount);
    }

    [Fact]
    public async Task SuccessfulOtpRequestCreatesHashedWithdrawalCode()
    {
        var context = CreateContext(seedWithdrawalOtp: false);

        var response = await context.Service.RequestOtpAsync(
            context.DriverId,
            ValidOtpRequest(context));

        var verificationCode = Assert.Single(
            context.UnitOfWork.VerificationCodeRepository.Items);
        var rawCode = Assert.Single(context.EmailService.Codes);
        Assert.Equal(
            VerificationCodePurpose.WalletWithdrawal,
            verificationCode.Purpose);
        Assert.Equal(
            VerificationChannel.Email,
            verificationCode.Channel);
        Assert.DoesNotContain(rawCode, verificationCode.CodeHash);
        Assert.Equal(64, verificationCode.CodeHash.Length);
        Assert.InRange(
            verificationCode.ExpiresAt,
            DateTime.UtcNow.AddMinutes(9),
            DateTime.UtcNow.AddMinutes(10));
        Assert.Equal(
            verificationCode.ExpiresAt,
            response.ExpiresAt);
        Assert.Equal(
            verificationCode.LastSentAt.AddSeconds(60),
            response.ResendAvailableAt);
        Assert.DoesNotContain(
            rawCode,
            System.Text.Json.JsonSerializer.Serialize(response));
    }

    [Fact]
    public async Task NonDriverCannotRequestWithdrawalOtp()
    {
        var context = CreateContext(seedWithdrawalOtp: false);
        context.UnitOfWork.UserRoleRepository.Items.Clear();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            context.Service.RequestOtpAsync(
                context.DriverId,
                ValidOtpRequest(context)));

        Assert.Empty(context.EmailService.Codes);
    }

    [Fact]
    public async Task InsufficientBalanceRejectsOtpRequestBeforeGeneration()
    {
        var context = CreateContext(seedWithdrawalOtp: false);
        context.Wallet.Balance = 100m;

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.RequestOtpAsync(
                context.DriverId,
                ValidOtpRequest(context)));

        Assert.Empty(context.EmailService.Codes);
        Assert.Empty(
            context.UnitOfWork.VerificationCodeRepository.Items);
    }

    [Fact]
    public async Task InactiveBankAccountRejectsOtpRequest()
    {
        var context = CreateContext(seedWithdrawalOtp: false);
        context.BankAccount.IsActive = false;

        await Assert.ThrowsAsync<NotFoundException>(() =>
            context.Service.RequestOtpAsync(
                context.DriverId,
                ValidOtpRequest(context)));

        Assert.Empty(context.EmailService.Codes);
    }

    [Fact]
    public async Task MissingRecipientCodeRejectsOtpRequest()
    {
        var context = CreateContext(seedWithdrawalOtp: false);
        context.BankAccount.RecipientCode = string.Empty;

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.RequestOtpAsync(
                context.DriverId,
                ValidOtpRequest(context)));

        Assert.Empty(context.EmailService.Codes);
    }

    [Fact]
    public async Task OtpRequestEnforcesResendCooldown()
    {
        var context = CreateContext(seedWithdrawalOtp: false);
        await context.Service.RequestOtpAsync(
            context.DriverId,
            ValidOtpRequest(context));

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.RequestOtpAsync(
                context.DriverId,
                ValidOtpRequest(context)));

        Assert.Single(context.EmailService.Codes);
        Assert.Single(
            context.UnitOfWork.VerificationCodeRepository.Items);
    }

    [Fact]
    public async Task OtpRequestEnforcesFivePerHourLimit()
    {
        var context = CreateContext(seedWithdrawalOtp: false);

        for (var index = 0; index < 5; index++)
        {
            var code = WithdrawalCode(
                context.DriverId,
                $"{index + 1:000000}");
            code.CreatedAt = DateTime.UtcNow.AddMinutes(-5 - index);
            code.LastSentAt = code.CreatedAt;
            code.ConsumedAt = code.CreatedAt.AddSeconds(1);
            context.UnitOfWork.VerificationCodeRepository.Items.Add(code);
        }

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.RequestOtpAsync(
                context.DriverId,
                ValidOtpRequest(context)));

        Assert.Empty(context.EmailService.Codes);
        Assert.Equal(
            5,
            context.UnitOfWork.VerificationCodeRepository.Items.Count);
    }

    [Fact]
    public async Task NewOtpInvalidatesPreviousUnusedOtp()
    {
        var context = CreateContext(seedWithdrawalOtp: false);
        var previousCode = WithdrawalCode(
            context.DriverId,
            "111111");
        previousCode.CreatedAt = DateTime.UtcNow.AddMinutes(-2);
        previousCode.LastSentAt = DateTime.UtcNow.AddSeconds(-61);
        context.UnitOfWork.VerificationCodeRepository.Items.Add(
            previousCode);

        await context.Service.RequestOtpAsync(
            context.DriverId,
            ValidOtpRequest(context));

        Assert.NotNull(previousCode.ConsumedAt);
        Assert.Equal(
            2,
            context.UnitOfWork.VerificationCodeRepository.Items.Count);
    }

    [Fact]
    public async Task WrongOtpIncrementsAttemptAndDoesNotCallPaystack()
    {
        var context = CreateContext();
        var request = ValidRequest(context);
        request.Otp = "000000";

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.CreateAsync(
                context.DriverId,
                request));

        Assert.Equal(
            1,
            context.UnitOfWork.VerificationCodeRepository
                .Items.Single().AttemptCount);
        Assert.Equal(0, context.PaystackClient.TransferCallCount);
    }

    [Fact]
    public async Task FiveWrongOtpAttemptsLockTheCode()
    {
        var context = CreateContext();
        var request = ValidRequest(context);
        request.Otp = "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                context.Service.CreateAsync(
                    context.DriverId,
                    request));
        }

        var verificationCode = context.UnitOfWork
            .VerificationCodeRepository.Items.Single();
        Assert.Equal(5, verificationCode.AttemptCount);
        Assert.NotNull(verificationCode.ConsumedAt);
        Assert.Equal(0, context.PaystackClient.TransferCallCount);
    }

    [Fact]
    public async Task ExpiredWithdrawalOtpIsRejected()
    {
        var context = CreateContext();
        context.UnitOfWork.VerificationCodeRepository
            .Items.Single().ExpiresAt = DateTime.UtcNow.AddSeconds(-1);

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.CreateAsync(
                context.DriverId,
                ValidRequest(context)));

        Assert.Equal(0, context.PaystackClient.TransferCallCount);
    }

    [Fact]
    public async Task ConsumedWithdrawalOtpIsRejected()
    {
        var context = CreateContext();
        context.UnitOfWork.VerificationCodeRepository
            .Items.Single().ConsumedAt = DateTime.UtcNow;

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.CreateAsync(
                context.DriverId,
                ValidRequest(context)));

        Assert.Equal(0, context.PaystackClient.TransferCallCount);
    }

    [Fact]
    public async Task EmailVerificationOtpCannotAuthorizeWithdrawal()
    {
        var context = CreateContext();
        context.UnitOfWork.VerificationCodeRepository
            .Items.Single().Purpose =
                VerificationCodePurpose.EmailAccountVerification;

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.CreateAsync(
                context.DriverId,
                ValidRequest(context)));

        Assert.Equal(0, context.PaystackClient.TransferCallCount);
    }

    [Fact]
    public async Task ConsumedOtpCannotBeReused()
    {
        var context = CreateContext();
        var request = ValidRequest(context);

        await context.Service.CreateAsync(
            context.DriverId,
            request);

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.CreateAsync(
                context.DriverId,
                request));

        Assert.Equal(1, context.PaystackClient.TransferCallCount);
        Assert.Single(
            context.UnitOfWork.WalletTransactionRepository.Items);
    }

    private static WithdrawalTestContext CreateContext(
        bool seedWithdrawalOtp = true)
    {
        var unitOfWork = new TestUnitOfWork();
        var driverId = Guid.NewGuid();
        var driver = new User
        {
            Id = driverId,
            Email = "driver@test.local",
            IsEmailVerified = true,
            Status = UserStatus.Active
        };
        var wallet = new Wallet
        {
            UserId = driverId,
            Balance = 2000m
        };
        var bankAccount = new DriverBankAccount
        {
            UserId = driverId,
            BankCode = "058",
            BankName = "Guaranty Trust Bank",
            AccountNumber = "0123456789",
            AccountName = "Example Account Name",
            RecipientCode = "RCP_test_recipient",
            IsActive = true
        };
        unitOfWork.UserRepository.Items.Add(driver);
        var driverRole = ((TestRoleRepository)unitOfWork.Roles)
            .Items.Single(role => role.Name == "Driver");
        unitOfWork.UserRoleRepository.Items.Add(new UserRole
        {
            UserId = driverId,
            User = driver,
            RoleId = driverRole.Id,
            Role = driverRole
        });
        unitOfWork.WalletRepository.Items.Add(wallet);
        unitOfWork.DriverBankAccountRepository.Items.Add(bankAccount);

        if (seedWithdrawalOtp)
        {
            unitOfWork.VerificationCodeRepository.Items.Add(
                WithdrawalCode(driverId, "123456"));
        }

        var paystackClient = new FakePaystackClient();
        var emailService = new CapturingEmailService();
        var financialService = new FinancialService(unitOfWork);
        var service = new DriverWithdrawalService(
            emailService,
            financialService,
            NullLogger<DriverWithdrawalService>.Instance,
            paystackClient,
            unitOfWork);

        return new WithdrawalTestContext(
            unitOfWork,
            driverId,
            wallet,
            bankAccount,
            emailService,
            paystackClient,
            service);
    }

    private static CreateDriverWithdrawalRequestDto ValidRequest(
        WithdrawalTestContext context)
    {
        return new CreateDriverWithdrawalRequestDto
        {
            DriverBankAccountId = context.BankAccount.Id,
            Amount = 500m,
            Otp = "123456"
        };
    }

    private static DriverWithdrawalOtpRequestDto ValidOtpRequest(
        WithdrawalTestContext context)
    {
        return new DriverWithdrawalOtpRequestDto
        {
            DriverBankAccountId = context.BankAccount.Id,
            Amount = 500m
        };
    }

    private static VerificationCode WithdrawalCode(
        Guid userId,
        string code)
    {
        var purpose = VerificationCodePurpose.WalletWithdrawal;
        var value = $"{userId:N}:{purpose}:{code}";
        var hash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value)));
        var now = DateTime.UtcNow;

        return new VerificationCode
        {
            UserId = userId,
            Purpose = purpose,
            Channel = VerificationChannel.Email,
            CodeHash = hash,
            ExpiresAt = now.AddMinutes(10),
            LastSentAt = now,
            CreatedAt = now
        };
    }

    private sealed class FakePaystackClient : IPaystackClient
    {
        private int _transferCallCount;

        public Exception? Failure { get; set; }
        public string Status { get; set; } = "success";
        public int TransferCallCount => _transferCallCount;
        public string RecipientCode { get; private set; } = string.Empty;
        public long AmountInKobo { get; private set; }
        public string Reference { get; private set; } = string.Empty;
        public string Reason { get; private set; } = string.Empty;

        public Task<IReadOnlyList<PaystackBank>> GetBanksAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PaystackResolvedAccount> ResolveAccountAsync(
            string bankCode,
            string accountNumber,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PaystackTransferRecipient> CreateTransferRecipientAsync(
            string bankCode,
            string accountNumber,
            string accountName,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PaystackTransferResult> CreateTransferAsync(
            string recipientCode,
            long amountInKobo,
            string reference,
            string reason,
            CancellationToken cancellationToken = default)
        {
            if (Failure != null)
            {
                throw Failure;
            }

            RecipientCode = recipientCode;
            AmountInKobo = amountInKobo;
            Reference = reference;
            Reason = reason;

            Interlocked.Increment(ref _transferCallCount);

            return Task.FromResult(
                new PaystackTransferResult
                {
                    Reference = reference,
                    Status = Status,
                    TransferCode = "TRF_test"
                });
        }
    }

    private sealed class CapturingEmailService : IEmailService
    {
        public List<string> Codes { get; } = [];

        public Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
        {
            var match = Regex.Match(htmlBody, @"\b\d{6}\b");
            Codes.Add(match.Value);
            return Task.CompletedTask;
        }
    }

    private sealed class WithdrawalTestContext
    {
        public WithdrawalTestContext(
            TestUnitOfWork unitOfWork,
            Guid driverId,
            Wallet wallet,
            DriverBankAccount bankAccount,
            CapturingEmailService emailService,
            FakePaystackClient paystackClient,
            DriverWithdrawalService service)
        {
            UnitOfWork = unitOfWork;
            DriverId = driverId;
            Wallet = wallet;
            BankAccount = bankAccount;
            EmailService = emailService;
            PaystackClient = paystackClient;
            Service = service;
        }

        public TestUnitOfWork UnitOfWork { get; }
        public Guid DriverId { get; }
        public Wallet Wallet { get; }
        public DriverBankAccount BankAccount { get; }
        public CapturingEmailService EmailService { get; }
        public FakePaystackClient PaystackClient { get; }
        public DriverWithdrawalService Service { get; }
    }
}
