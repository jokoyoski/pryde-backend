using Pryde.Contracts.ResponseModels;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class UserService(IUnitOfWork unitOfWork) : IUserService
{
    public async Task<IReadOnlyList<UserSummaryResponseDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await unitOfWork.Users.GetAllAsync(cancellationToken);

        var result = new List<UserSummaryResponseDto>();

        foreach (var user in users)
        {
            var userRoles = await unitOfWork.UserRoles.GetByUserIdAsync(user.Id, cancellationToken);
            var profile = await unitOfWork.Profiles.GetByUserIdAsync(user.Id, cancellationToken);
            var kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(user.Id, cancellationToken);

            result.Add(new UserSummaryResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                FirstName = profile?.FirstName ?? string.Empty,
                LastName = profile?.LastName ?? string.Empty,
                Status = user.Status.ToString(),
                IsEmailVerified = user.IsEmailVerified,
                IsPhoneNumberVerified = user.IsPhoneNumberVerified,
                KycStatus = kyc?.Status.ToString() ?? "NotStarted",
                Roles = userRoles.Select(ur => ur.Role.Name).Distinct().ToList(),
                CreatedAt = user.CreatedAt
            });
        }

        return result;
    }
}