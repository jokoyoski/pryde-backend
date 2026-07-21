using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Api.Authorization;

public sealed class EmailVerifiedHandler(
    IUnitOfWork unitOfWork,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<EmailVerifiedRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EmailVerifiedRequirement requirement)
    {
        if (!Guid.TryParse(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return;
        }

        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted
                                ?? CancellationToken.None;
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user?.IsEmailVerified == true)
        {
            context.Succeed(requirement);
        }
    }
}
