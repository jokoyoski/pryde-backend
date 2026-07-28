using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IDriverBankAccountRepository
{
    Task<IReadOnlyList<DriverBankAccount>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid userId,
        string bankCode,
        string accountNumber,
        CancellationToken cancellationToken = default);

    Task<bool> HasAnyActiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<DriverBankAccount> CreateAsync(
        DriverBankAccount bankAccount,
        CancellationToken cancellationToken = default);
}
