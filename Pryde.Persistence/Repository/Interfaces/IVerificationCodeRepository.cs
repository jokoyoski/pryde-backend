using Pryde.Domain.Entities;
using Pryde.Domain.Enums;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IVerificationCodeRepository
{
    Task<VerificationCode?> GetLatestAsync(
        Guid userId,
        VerificationCodePurpose purpose,
        VerificationChannel channel,
        CancellationToken cancellationToken = default);

    Task<int> CountCreatedSinceAsync(
        Guid userId,
        VerificationCodePurpose purpose,
        VerificationChannel channel,
        DateTime createdSince,
        CancellationToken cancellationToken = default);

    Task InvalidateUnusedAsync(
        Guid userId,
        VerificationCodePurpose purpose,
        VerificationChannel channel,
        DateTime consumedAt,
        CancellationToken cancellationToken = default);

    Task<VerificationCode> CreateAsync(
        VerificationCode verificationCode,
        CancellationToken cancellationToken = default);

    void Update(VerificationCode verificationCode);
}
