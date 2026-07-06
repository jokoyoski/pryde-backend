using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task<RefreshToken> CreateAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken = default);

    void Revoke(RefreshToken refreshToken);

    Task RevokeAllActiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}