using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class PasswordResetCodeRepository(PrydeDbContext context)
    : IPasswordResetCodeRepository
{
    public async Task<PasswordResetCode?> GetLatestActiveByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.PasswordResetCodes
            .Where(c => c.UserId == userId && c.UsedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PasswordResetCode> CreateAsync(
        PasswordResetCode code, CancellationToken cancellationToken = default)
    {
        await context.PasswordResetCodes.AddAsync(code, cancellationToken);
        return code;
    }

    public void MarkUsed(PasswordResetCode code)
    {
        code.UsedAt = DateTime.UtcNow;
        context.PasswordResetCodes.Update(code);
    }

    public async Task InvalidateAllForUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var codes = await context.PasswordResetCodes
            .Where(c => c.UserId == userId && c.UsedAt == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var code in codes) code.UsedAt = now;
    }
}