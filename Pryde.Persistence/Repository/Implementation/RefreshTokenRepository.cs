using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class RefreshTokenRepository(PrydeDbContext context) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return await context.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(
                token => token.TokenHash == tokenHash,
                cancellationToken);
    }

    public async Task<RefreshToken> CreateAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken = default)
    {
        await context.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        return refreshToken;
    }

    public void Revoke(RefreshToken refreshToken)
    {
        refreshToken.RevokedAt = DateTime.UtcNow;
        context.RefreshTokens.Update(refreshToken);
    }

    public async Task RevokeAllActiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var activeTokens = await context.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null &&
            token.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }
    }
}