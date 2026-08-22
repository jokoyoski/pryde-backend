using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Providers.Kyc;

public sealed class KycAttemptLimitExceededException(
    KycAttemptAllowanceResponseDto attemptAllowance)
    : Exception("You have reached your monthly KYC attempt limit.")
{
    public KycAttemptAllowanceResponseDto AttemptAllowance { get; } =
        attemptAllowance;
}
