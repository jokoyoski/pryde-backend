using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IUserRepository
{
    Task<bool> ExistsByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<User?> GetByPhoneNumberAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string email,
        string? phoneNumber,
        CancellationToken cancellationToken = default);

    Task<bool> HasProtectedDeletionRecordsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task DeleteWithRelatedDataAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<User> CreateAsync(
        User user,
        CancellationToken cancellationToken = default);

    void Update(User user);

    void Delete(User user);
}
