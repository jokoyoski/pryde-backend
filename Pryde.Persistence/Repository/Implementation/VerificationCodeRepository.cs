using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class VerificationCodeRepository(PrydeDbContext context)
    : IVerificationCodeRepository
{
    public Task<VerificationCode?> GetLatestActiveAsync(
        Guid userId,
        VerificationCodePurpose purpose,
        VerificationChannel channel,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return context.VerificationCodes
            .Where(code => code.UserId == userId &&
                           code.Purpose == purpose &&
                           code.Channel == channel &&
                           code.ConsumedAt == null &&
                           code.ExpiresAt > now)
            .OrderByDescending(code => code.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountCreatedSinceAsync(
        Guid userId,
        VerificationCodePurpose purpose,
        VerificationChannel channel,
        DateTime createdSince,
        CancellationToken cancellationToken = default) =>
        context.VerificationCodes.CountAsync(code =>
            code.UserId == userId &&
            code.Purpose == purpose &&
            code.Channel == channel &&
            code.CreatedAt >= createdSince,
            cancellationToken);

    public async Task InvalidateUnusedAsync(
        Guid userId,
        VerificationCodePurpose purpose,
        VerificationChannel channel,
        DateTime consumedAt,
        CancellationToken cancellationToken = default)
    {
        var codes = await context.VerificationCodes
            .Where(code => code.UserId == userId &&
                           code.Purpose == purpose &&
                           code.Channel == channel &&
                           code.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var code in codes)
        {
            code.ConsumedAt = consumedAt;
        }
    }

    public async Task<VerificationCode> CreateAsync(
        VerificationCode verificationCode,
        CancellationToken cancellationToken = default)
    {
        await context.VerificationCodes.AddAsync(verificationCode, cancellationToken);
        return verificationCode;
    }

    public void Update(VerificationCode verificationCode) =>
        context.VerificationCodes.Update(verificationCode);
}
