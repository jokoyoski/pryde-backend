using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

namespace Pryde.Api.Extensions;

public static class ApiVersioningExtensions
{
    public static IServiceCollection AddApiVersioningConfiguration(
        this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }
}