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
}
