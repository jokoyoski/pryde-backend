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
            .GetSection(DatabaseSettings.SectionName)
            .Get<DatabaseSettings>()
            ?? throw new InvalidOperationException(
                "PrydeConnection configuration is missing.");

        var host = GetRequiredTrimmedValue(settings.Host, "Host");
        var database = GetRequiredTrimmedValue(settings.Database, "Database");
        var username = GetRequiredTrimmedValue(settings.Username, "Username");
        var sslMode = GetRequiredTrimmedValue(settings.SslMode, "SslMode");

        if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
        {
            throw new InvalidOperationException(
                "PrydeConnection Host must be a hostname without a scheme, port, quotes, or path.");
        }

        if (settings.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "PrydeConnection Port must be between 1 and 65535.");
        }

        if (string.IsNullOrEmpty(settings.Password))
        {
            throw new InvalidOperationException(
                "PrydeConnection Password is required.");
        }

        if (!Enum.TryParse<SslMode>(sslMode, true, out var parsedSslMode))
        {
            throw new InvalidOperationException(
                "PrydeConnection SslMode is invalid.");
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Database = database,
            Password = settings.Password,
            Username = username,
            Port = settings.Port,

            IncludeErrorDetail = true,
            Pooling = true,

            SslMode = parsedSslMode
        };
    }

    private static string GetRequiredTrimmedValue(string? value, string name)
    {
        var trimmedValue = value?.Trim();
        if (string.IsNullOrEmpty(trimmedValue))
        {
            throw new InvalidOperationException(
                $"PrydeConnection {name} is required.");
        }

        return trimmedValue;
    }
}
