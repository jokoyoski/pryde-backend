using Mapster;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Providers.Paystack;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class DriverBankAccountService : IDriverBankAccountService
{
    private readonly IPaystackClient _paystackClient;
    private readonly IUnitOfWork _unitOfWork;

    public DriverBankAccountService(
        IPaystackClient paystackClient,
        IUnitOfWork unitOfWork)
    {
        _paystackClient = paystackClient;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<BankResponseDto>> GetBanksAsync(
        CancellationToken cancellationToken = default)
    {
        var providerBanks = await _paystackClient.GetBanksAsync(
            cancellationToken);
        var banks = new List<BankResponseDto>();

        foreach (var providerBank in providerBanks)
        {
            banks.Add(new BankResponseDto
            {
                Name = providerBank.Name,
                Code = providerBank.Code
            });
        }

        return banks;
    }

    public async Task<ResolvedBankAccountResponseDto> ResolveAccountAsync(
        ResolveBankAccountRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(
            request?.BankCode,
            request?.AccountNumber);

        var bankCode = request!.BankCode.Trim();
        var accountNumber = request.AccountNumber.Trim();

        await GetSelectedBankAsync(
            bankCode,
            cancellationToken);

        var resolvedAccount = await _paystackClient.ResolveAccountAsync(
            bankCode,
            accountNumber,
            cancellationToken);

        ValidateResolvedAccount(
            resolvedAccount.AccountNumber,
            resolvedAccount.AccountName,
            accountNumber);

        return new ResolvedBankAccountResponseDto
        {
            BankCode = bankCode,
            AccountNumber = MaskAccountNumber(accountNumber),
            AccountName = resolvedAccount.AccountName.Trim()
        };
    }

    public async Task<DriverBankAccountResponseDto> CreateAsync(
        Guid userId,
        CreateDriverBankAccountRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(
            request?.BankCode,
            request?.AccountNumber);

        await EnsureDriverAsync(userId, cancellationToken);

        var bankCode = request!.BankCode.Trim();
        var accountNumber = request.AccountNumber.Trim();

        var accountExists = await _unitOfWork.DriverBankAccounts.ExistsAsync(
            userId,
            bankCode,
            accountNumber,
            cancellationToken);

        if (accountExists)
        {
            throw new ConflictException(
                "This bank account has already been saved.");
        }

        var selectedBank = await GetSelectedBankAsync(
            bankCode,
            cancellationToken);

        var resolvedAccount = await _paystackClient.ResolveAccountAsync(
            bankCode,
            accountNumber,
            cancellationToken);

        ValidateResolvedAccount(
            resolvedAccount.AccountNumber,
            resolvedAccount.AccountName,
            accountNumber);

        var transferRecipient = await _paystackClient
            .CreateTransferRecipientAsync(
                bankCode,
                accountNumber,
                resolvedAccount.AccountName.Trim(),
                cancellationToken);

        if (string.IsNullOrWhiteSpace(
                transferRecipient.RecipientCode))
        {
            throw new ServiceUnavailableException(
                "Paystack did not create a transfer recipient.");
        }

        var bankAccount = new DriverBankAccount
        {
            UserId = userId,
            BankCode = bankCode,
            BankName = selectedBank.Name,
            AccountNumber = accountNumber,
            AccountName = resolvedAccount.AccountName.Trim(),
            RecipientCode = transferRecipient.RecipientCode.Trim(),
            IsDefault = true,
            IsActive = true
        };

        return await _unitOfWork.ExecuteInTransactionOnceAsync(
            async transactionToken =>
            {
                var activeAccounts = await _unitOfWork.DriverBankAccounts
                    .GetActiveByUserIdForUpdateAsync(
                        userId,
                        transactionToken);

                foreach (var activeAccount in activeAccounts)
                {
                    activeAccount.IsActive = false;
                    activeAccount.IsDefault = false;
                    _unitOfWork.DriverBankAccounts.Update(activeAccount);
                }

                await _unitOfWork.DriverBankAccounts.CreateAsync(
                    bankAccount,
                    transactionToken);
                await _unitOfWork.SaveChangesAsync(transactionToken);

                return bankAccount.Adapt<DriverBankAccountResponseDto>();
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<DriverBankAccountResponseDto>> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureDriverAsync(userId, cancellationToken);

        var bankAccounts = await _unitOfWork.DriverBankAccounts
            .GetByUserIdAsync(userId, cancellationToken);

        return bankAccounts.Adapt<List<DriverBankAccountResponseDto>>();
    }

    private async Task<PaystackBank> GetSelectedBankAsync(
        string bankCode,
        CancellationToken cancellationToken)
    {
        var providerBanks = await _paystackClient.GetBanksAsync(
            cancellationToken);

        var selectedBank = providerBanks.FirstOrDefault(bank =>
            bank.Code.Equals(
                bankCode,
                StringComparison.OrdinalIgnoreCase));

        if (selectedBank == null)
        {
            throw new ValidationException("Bank code is invalid.");
        }

        return selectedBank;
    }

    private async Task EnsureDriverAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var userRoles = await _unitOfWork.UserRoles.GetByUserIdAsync(
            userId,
            cancellationToken);

        var isDriver = userRoles.Any(userRole =>
            userRole.Role.Name == RoleNames.Driver);

        if (!isDriver)
        {
            throw new ForbiddenException(
                "Only drivers can manage withdrawal bank accounts.");
        }
    }

    private static void ValidateRequest(
        string? bankCode,
        string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(bankCode))
        {
            throw new ValidationException("Bank code is required.");
        }

        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            throw new ValidationException(
                "Account number is required.");
        }

        var trimmedAccountNumber = accountNumber.Trim();

        if (trimmedAccountNumber.Length != 10)
        {
            throw new ValidationException(
                "Account number must contain 10 digits.");
        }

        foreach (var character in trimmedAccountNumber)
        {
            if (!char.IsDigit(character))
            {
                throw new ValidationException(
                    "Account number must contain only digits.");
            }
        }
    }

    private static void ValidateResolvedAccount(
        string resolvedAccountNumber,
        string accountName,
        string requestedAccountNumber)
    {
        if (!resolvedAccountNumber.Equals(
                requestedAccountNumber,
                StringComparison.Ordinal))
        {
            throw new ServiceUnavailableException(
                "Paystack returned different account details.");
        }

        if (string.IsNullOrWhiteSpace(accountName))
        {
            throw new ServiceUnavailableException(
                "Paystack did not return an account name.");
        }
    }

    private static string MaskAccountNumber(string accountNumber)
    {
        return $"******{accountNumber[^4..]}";
    }
}
