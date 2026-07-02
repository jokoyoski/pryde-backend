using Mapster;
using Pryde.Domain.Common.Exceptions;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Entities;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;
namespace Pryde.Services.Service.Implementation;
public class ProfileService(IUnitOfWork unitOfWork) : IProfileService
{
    public async Task<ProfileResponseDto> GetMineAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await unitOfWork.Profiles.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Profile), userId);
        return profile.Adapt<ProfileResponseDto>();
    }

    public async Task<ProfileResponseDto> UpdateAsync(Guid userId, ProfileUpdateRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName))
            throw new ValidationException("First name is required.");
        if (string.IsNullOrWhiteSpace(request.LastName))
            throw new ValidationException("Last name is required.");

        var profile = await unitOfWork.Profiles.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Profile), userId);

        profile.FirstName = request.FirstName.Trim();
        profile.LastName = request.LastName.Trim();

        unitOfWork.Profiles.Update(profile);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return profile.Adapt<ProfileResponseDto>();
    }

    public async Task<ProfileResponseDto> UpdatePhotoAsync(Guid userId, string photoUrl, CancellationToken cancellationToken = default)
    {
        var profile = await unitOfWork.Profiles.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Profile), userId);

        profile.ProfilePhotoUrl = photoUrl;

        unitOfWork.Profiles.Update(profile);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return profile.Adapt<ProfileResponseDto>();
    }
}