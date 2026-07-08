using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IPasswordResetCodeRepository
{
    Task<PasswordResetCode?> GetLatestActiveByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default);

    Task<PasswordResetCode> CreateAsync(
        PasswordResetCode code, CancellationToken cancellationToken = default);

    void MarkUsed(PasswordResetCode code);

    Task InvalidateAllForUserAsync(
        Guid userId, CancellationToken cancellationToken = default);
}