using Microsoft.Extensions.Configuration;
using Npgsql;
using Pryde.Persistence.Settings;

namespace Pryde.Persistence.Extension;

public static class NpgsqlConnectionExtension
{
    public static NpgsqlConnectionStringBuilder GetDbConnectionStringBuilder(
        this IConfiguration configuration)
    {
        var settings = configuration
            .GetSection("PrydeConnection")
            .Get<DatabaseSettings>()
            ?? throw new InvalidOperationException(
                "PrydeConnection configuration is missing.");

        return new NpgsqlConnectionStringBuilder
        {
            Host = settings.Host,
            Database = settings.Database,
            Password = settings.Password,
            Username = settings.Username,
            Port = settings.Port,

            IncludeErrorDetail = true,
            Pooling = true,

            SslMode = Enum.TryParse<SslMode>(
                settings.SslMode,
                true,
                out var parsedSslMode)
                ? parsedSslMode
                : SslMode.Require
        };
    }
}