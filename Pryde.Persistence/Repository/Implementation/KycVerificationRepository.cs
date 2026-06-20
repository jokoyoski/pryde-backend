using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class KycVerificationRepository(PrydeDbContext context)
    : IKycVerificationRepository
{
    public async Task<KycVerification?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await context.KycVerifications
            .AsNoTracking()
            .FirstOrDefaultAsync(kyc => kyc.Id == id, cancellationToken);
    }

    public async Task<KycVerification?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await context.KycVerifications
            .AsNoTracking()
            .FirstOrDefaultAsync(kyc => kyc.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<KycVerification>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.KycVerifications
            .AsNoTracking()
            .OrderByDescending(kyc => kyc.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KycVerification>> GetByStatusAsync(
    KycStatus status,
    CancellationToken cancellationToken = default)
    {
        return await context.KycVerifications
            .AsNoTracking()
            .Where(k => k.Status == status)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await context.KycVerifications
            .AnyAsync(kyc => kyc.UserId == userId, cancellationToken);
    }

    public async Task<KycVerification> CreateAsync(
        KycVerification kycVerification,
        CancellationToken cancellationToken = default)
    {
        await context.KycVerifications.AddAsync(kycVerification, cancellationToken);
        return kycVerification;
    }

    public void Update(KycVerification kycVerification)
    {
        context.KycVerifications.Update(kycVerification);
    }

    public void Delete(KycVerification kycVerification)
    {
        context.KycVerifications.Remove(kycVerification);
    }
}
