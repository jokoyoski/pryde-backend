using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Pryde.Api.Authorization;
using Pryde.Domain.Entities;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Api;

public class EmailVerificationAuthorizationTests
{
    [Fact]
    public async Task RestrictedPolicyRejectsUnverifiedUser()
    {
        var result = await AuthorizeAsync(isEmailVerified: false);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RestrictedPolicyAllowsVerifiedUser()
    {
        var result = await AuthorizeAsync(isEmailVerified: true);

        Assert.True(result.Succeeded);
    }

    private static async Task<AuthorizationResult> AuthorizeAsync(bool isEmailVerified)
    {
        var unitOfWork = new TestUnitOfWork();
        var user = new User
        {
            Email = "policy@test.local",
            PhoneNumber = "08000000000",
            IsEmailVerified = isEmailVerified
        };
        ((TestUserRepository)unitOfWork.Users).Items.Add(user);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IUnitOfWork>(unitOfWork);
        services.AddHttpContextAccessor();
        services.AddScoped<IAuthorizationHandler, EmailVerifiedHandler>();
        services.AddAuthorization(options => options.AddPolicy(
            AuthorizationPolicies.EmailVerified,
            policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new EmailVerifiedRequirement())));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())],
            "Test"));
        return await scope.ServiceProvider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, null, AuthorizationPolicies.EmailVerified);
    }
}
