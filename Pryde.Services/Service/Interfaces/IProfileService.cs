using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;
public interface IProfileService
{
    Task<ProfileResponseDto> GetMineAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ProfileResponseDto> UpdateAsync(Guid userId, ProfileUpdateRequestDto request, CancellationToken cancellationToken = default);
    Task<ProfileResponseDto> UpdatePhotoAsync(Guid userId, string photoUrl, CancellationToken cancellationToken = default);
}