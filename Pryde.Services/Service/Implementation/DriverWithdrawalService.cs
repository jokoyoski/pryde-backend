using Mapster;
using Microsoft.Extensions.Logging;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Notifications.Interface;
using Pryde.Services.Providers.Paystack;
using Pryde.Services.Security.Implementation;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class DriverWithdrawalService : IDriverWithdrawalService
{
    private const string Currency = "NGN";
    private const string WithdrawalReason = "Pryde driver withdrawal";
    private const int MaximumOtpAttempts = 5;
    private const int MaximumOtpRequestsPerHour = 5;
    private const int OtpExpiryMinutes = 10;
    private const int ResendCooldownSeconds = 60;
    private readonly IEmailService _emailService;
    private readonly IFinancialService _financialService;
    private readonly ILogger<DriverWithdrawalService> _logger;
    private readonly IPaystackClient _paystackClient;
    private readonly IUnitOfWork _unitOfWork;

    public DriverWithdrawalService(
        IEmailService emailService,
        IFinancialService financialService,
        ILogger<DriverWithdrawalService> logger,
        IPaystackClient paystackClient,
        IUnitOfWork unitOfWork)
    {
        _emailService = emailService;
        _financialService = financialService;
        _logger = logger;
        _paystackClient = paystackClient;
        _unitOfWork = unitOfWork;
    }

    public async Task<DriverWithdrawalOtpResponseDto> RequestOtpAsync(
        Guid userId,
        DriverWithdrawalOtpRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ValidationException(
                "Request cannot be null.");
        }

        ValidateWithdrawalDetails(
            request.DriverBankAccountId,
            request.Amount);

        var user = await GetDriverAsync(userId, cancellationToken);
        await GetWithdrawalDetailsAsync(
            userId,
            request.DriverBankAccountId,
            request.Amount,
            cancellationToken);

        var result = await _unitOfWork.ExecuteInTransactionAsync(
            async transactionToken =>
            {
                var now = DateTime.UtcNow;
                var latestCode = await _unitOfWork.VerificationCodes
                    .GetLatestActiveAsync(
                        userId,
                        VerificationCodePurpose.WalletWithdrawal,
                        VerificationChannel.Email,
                        transactionToken);

                if (latestCode != null &&
                    latestCode.LastSentAt.AddSeconds(
                        ResendCooldownSeconds) > now)
                {
                    throw new ConflictException(
                        "Please wait before requesting another withdrawal code.");
                }

                var recentRequestCount = await _unitOfWork.VerificationCodes
                    .CountCreatedSinceAsync(
                        userId,
                        VerificationCodePurpose.WalletWithdrawal,
                        VerificationChannel.Email,
                        now.AddHours(-1),
                        transactionToken);

                if (recentRequestCount >= MaximumOtpRequestsPerHour)
                {
                    throw new ConflictException(
                        "The hourly withdrawal code request limit has been reached.");
                }

                await _unitOfWork.VerificationCodes.InvalidateUnusedAsync(
                    userId,
                    VerificationCodePurpose.WalletWithdrawal,
                    VerificationChannel.Email,
                    now,
                    transactionToken);

                var rawCode =
                    VerificationCodeSecurity.GenerateSixDigitCode();
                var verificationCode = new VerificationCode
                {
                    UserId = userId,
                    Purpose = VerificationCodePurpose.WalletWithdrawal,
                    Channel = VerificationChannel.Email,
                    CodeHash = VerificationCodeSecurity.Hash(
                        userId,
                        VerificationCodePurpose.WalletWithdrawal,
                        rawCode),
                    ExpiresAt = now.AddMinutes(OtpExpiryMinutes),
                    LastSentAt = now,
                    CreatedAt = now
                };

                await _unitOfWork.VerificationCodes.CreateAsync(
                    verificationCode,
                    transactionToken);
                await _unitOfWork.SaveChangesAsync(transactionToken);

                return (
                    VerificationCode: verificationCode,
                    RawCode: rawCode);
            },
            cancellationToken);

        try
        {
            await SendWithdrawalCodeAsync(
                user.Email,
                result.RawCode,
                cancellationToken);
        }
        catch (Exception exception)
        {
            result.VerificationCode.ConsumedAt = DateTime.UtcNow;
            _unitOfWork.VerificationCodes.Update(
                result.VerificationCode);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogError(
                exception,
                "Failed to send a withdrawal verification code for user {UserId}.",
                userId);

            throw new ServiceUnavailableException(
                "Withdrawal verification is temporarily unavailable.");
        }

        return new DriverWithdrawalOtpResponseDto
        {
            Message = "A withdrawal verification code has been sent.",
            ExpiresAt = result.VerificationCode.ExpiresAt,
            ResendAvailableAt = result.VerificationCode.LastSentAt
                .AddSeconds(ResendCooldownSeconds)
        };
    }

    public async Task<DriverWithdrawalResponseDto> CreateAsync(
        Guid userId,
        CreateDriverWithdrawalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        await GetDriverAsync(userId, cancellationToken);
        var otpIsValid = await ConsumeWithdrawalOtpAsync(
            userId,
            request.Otp,
            cancellationToken);

        if (!otpIsValid)
        {
            throw new ValidationException(
                "Invalid or expired withdrawal verification code.");
        }

        var bankAccount = await GetWithdrawalDetailsAsync(
            userId,
            request.DriverBankAccountId,
            request.Amount,
            cancellationToken);

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

        ValidateWithdrawalDetails(
            request.DriverBankAccountId,
            request.Amount);

        if (request.Otp == null ||
            request.Otp.Length != 6 ||
            !request.Otp.All(char.IsDigit))
        {
            throw new ValidationException(
                "A six-digit withdrawal verification code is required.");
        }
    }

    private async Task<User> GetDriverAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(
            userId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);
        var userRoles = await _unitOfWork.UserRoles.GetByUserIdAsync(
            userId,
            cancellationToken);
        var isDriver = userRoles.Any(userRole =>
            userRole.Role.Name == RoleNames.Driver);

        if (!isDriver)
        {
            throw new ForbiddenException(
                "Only drivers can withdraw from a driver wallet.");
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new ValidationException(
                "The driver email address is required.");
        }

        return user;
    }

    private async Task<DriverBankAccount> GetWithdrawalDetailsAsync(
        Guid userId,
        Guid driverBankAccountId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var bankAccount = await _unitOfWork.DriverBankAccounts
            .GetActiveByIdAndUserIdAsync(
                driverBankAccountId,
                userId,
                cancellationToken)
            ?? throw new NotFoundException(
                nameof(DriverBankAccount),
                driverBankAccountId);

        if (string.IsNullOrWhiteSpace(bankAccount.RecipientCode))
        {
            throw new ConflictException(
                "The selected bank account is not ready for withdrawals.");
        }

        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(
            userId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(Wallet), userId);

        if (wallet.Balance < amount)
        {
            throw new ConflictException(
                "The wallet balance is insufficient for this withdrawal.");
        }

        return bankAccount;
    }

    private async Task<bool> ConsumeWithdrawalOtpAsync(
        Guid userId,
        string otp,
        CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(
            async transactionToken =>
            {
                var verificationCode = await _unitOfWork
                    .VerificationCodes.GetLatestActiveAsync(
                        userId,
                        VerificationCodePurpose.WalletWithdrawal,
                        VerificationChannel.Email,
                        transactionToken);
                var now = DateTime.UtcNow;

                if (verificationCode == null ||
                    verificationCode.ConsumedAt.HasValue ||
                    verificationCode.ExpiresAt <= now ||
                    verificationCode.AttemptCount >= MaximumOtpAttempts)
                {
                    return false;
                }

                if (!VerificationCodeSecurity.Matches(
                        userId,
                        VerificationCodePurpose.WalletWithdrawal,
                        otp,
                        verificationCode.CodeHash))
                {
                    verificationCode.AttemptCount++;

                    if (verificationCode.AttemptCount >=
                        MaximumOtpAttempts)
                    {
                        verificationCode.ConsumedAt = now;
                    }

                    _unitOfWork.VerificationCodes.Update(
                        verificationCode);
                    await _unitOfWork.SaveChangesAsync(
                        transactionToken);
                    return false;
                }

                verificationCode.ConsumedAt = now;
                _unitOfWork.VerificationCodes.Update(verificationCode);
                await _unitOfWork.SaveChangesAsync(transactionToken);
                return true;
            },
            cancellationToken);
    }

    private Task SendWithdrawalCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken)
    {
        return _emailService.SendAsync(
            email,
            "Confirm your Pryde withdrawal",
            $"<p>Your withdrawal verification code is <strong>{code}</strong>. " +
            $"It expires in {OtpExpiryMinutes} minutes.</p>",
            cancellationToken);
    }

    private static void ValidateWithdrawalDetails(
        Guid driverBankAccountId,
        decimal amount)
    {
        if (driverBankAccountId == Guid.Empty)
        {
            throw new ValidationException(
                "Driver bank account ID is required.");
        }

        if (amount <= 0)
        {
            throw new ValidationException(
                "Amount must be greater than zero.");
        }
    }
}
