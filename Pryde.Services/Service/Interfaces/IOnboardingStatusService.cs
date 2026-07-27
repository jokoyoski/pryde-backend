using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IOnboardingStatusService
{
    Task<OnboardingStatusResponseDto> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
