using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class DriverBankAccountRepository : IDriverBankAccountRepository
{
    private readonly PrydeDbContext _context;

    public DriverBankAccountRepository(PrydeDbContext context)
    {
        _context = context;
    }

    public async Task<DriverBankAccount?> GetActiveByIdAndUserIdAsync(
        Guid bankAccountId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DriverBankAccounts
            .AsNoTracking()
            .Where(bankAccount =>
                bankAccount.Id == bankAccountId &&
                bankAccount.UserId == userId &&
                bankAccount.IsActive)
            .Select(bankAccount => new DriverBankAccount
            {
                Id = bankAccount.Id,
                UserId = bankAccount.UserId,
                BankName = bankAccount.BankName,
                AccountNumber = bankAccount.AccountNumber,
                AccountName = bankAccount.AccountName,
                RecipientCode = bankAccount.RecipientCode,
                IsActive = bankAccount.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DriverBankAccount>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DriverBankAccounts
            .AsNoTracking()
            .Where(bankAccount =>
                bankAccount.UserId == userId &&
                bankAccount.IsActive)
            .OrderByDescending(bankAccount => bankAccount.IsDefault)
            .ThenByDescending(bankAccount => bankAccount.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DriverBankAccount>>
        GetActiveByUserIdForUpdateAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        return await _context.DriverBankAccounts
            .Where(bankAccount =>
                bankAccount.UserId == userId &&
                bankAccount.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        Guid userId,
        string bankCode,
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.DriverBankAccounts
            .AsNoTracking()
            .AnyAsync(
                bankAccount =>
                    bankAccount.UserId == userId &&
                    bankAccount.BankCode == bankCode &&
                    bankAccount.AccountNumber == accountNumber,
                cancellationToken);
    }

    public async Task<bool> HasAnyActiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DriverBankAccounts
            .AsNoTracking()
            .AnyAsync(
                bankAccount =>
                    bankAccount.UserId == userId &&
                    bankAccount.IsActive,
                cancellationToken);
    }

    public async Task<DriverBankAccount> CreateAsync(
        DriverBankAccount bankAccount,
        CancellationToken cancellationToken = default)
    {
        await _context.DriverBankAccounts.AddAsync(
            bankAccount,
            cancellationToken);

        return bankAccount;
    }

    public void Update(DriverBankAccount bankAccount)
    {
        _context.DriverBankAccounts.Update(bankAccount);
    }
}
