using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Pryde.Contracts.ResponseModels;
using Pryde.Services.Settings;
using Pryde.Api.Controllers.Driver.Authorization;

namespace Pryde.Api.Extension;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddAuthenticationConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettingsSection = configuration.GetSection(JwtSettings.SectionName);

        services.Configure<JwtSettings>(jwtSettingsSection);
        services.Configure<CloudinarySettings>(
            configuration.GetSection(CloudinarySettings.SectionName));

        var jwtSettings = jwtSettingsSection.Get<JwtSettings>()
            ?? throw new InvalidOperationException(
                "JwtSettings configuration is missing.");

        if (string.IsNullOrWhiteSpace(jwtSettings.Key) ||
            string.IsNullOrWhiteSpace(jwtSettings.Issuer) ||
            string.IsNullOrWhiteSpace(jwtSettings.Audience))
        {
            throw new InvalidOperationException(
                "JwtSettings is incomplete.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        ValidIssuer = jwtSettings.Issuer,
                        ValidAudience = jwtSettings.Audience,

                        IssuerSigningKey =
                            new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(jwtSettings.Key))
                    };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await WriteErrorResponseAsync(
                            context.Response,
                            StatusCodes.Status401Unauthorized,
                            "Unauthorized.");
                    },
                    OnForbidden = context => WriteErrorResponseAsync(
                        context.Response,
                        StatusCodes.Status403Forbidden,
                        "Forbidden.")
                };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<IAuthorizationHandler, EmailVerifiedHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicies.EmailVerified,
                policy => policy.RequireAuthenticatedUser()
                    .AddRequirements(new EmailVerifiedRequirement()));
        });

        return services;
    }

    private static async Task WriteErrorResponseAsync(
        HttpResponse response,
        int statusCode,
        string message)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json";

        await response.WriteAsync(JsonSerializer.Serialize(
            new ErrorResponseDto
            {
                StatusCode = statusCode,
                Message = message
            }));
    }
}
