using Pryde.Domain.DTOs.RequestModels;
using Pryde.Domain.DTOs.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IAuthService
{
    Task<RegisterResponseDto> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default);

    Task<LoginResponseDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default);
}
