using System.Data.Common;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pryde.Persistence.Context;
using Pryde.Persistence.Migrations;
using Pryde.Persistence.Repository.Implementations;

namespace Pryde.Tests.Persistence;

public class BookingWindowMigrationTests
{
    [Fact]
    public void MigrationRenamesBothColumnsAndConvertsStoredHoursToMinutes()
    {
        var operations = GetUpOperations();
        var renames = operations.OfType<RenameColumnOperation>().ToList();
        var conversions = operations.OfType<SqlOperation>().ToList();

        Assert.Contains(
            renames,
            operation => operation.Table == "Trips" &&
                operation.Name == "BookingWindowHours" &&
                operation.NewName == "BookingWindowMinutes");
        Assert.Contains(
            renames,
            operation => operation.Table == "RecurringTrips" &&
                operation.Name == "BookingWindowHours" &&
                operation.NewName == "BookingWindowMinutes");
        Assert.Equal(2, conversions.Count);
        Assert.All(
            conversions,
            operation => Assert.Contains("* 60", operation.Sql));
    }

    [DatabaseFact]
    public async Task PostgreSqlMigrationSqlPreservesExistingWindowDurations()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddPersistence(configuration);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider
            .GetRequiredService<PrydeDbContext>();
        await context.Database.OpenConnectionAsync();
        await using var transaction = await context.Database
            .BeginTransactionAsync();

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TEMP TABLE "Trips" (
                "BookingWindowMinutes" integer NOT NULL,
                "DepartureTime" timestamp with time zone NOT NULL
            ) ON COMMIT DROP;
            CREATE TEMP TABLE "RecurringTrips" (
                "BookingWindowMinutes" integer NOT NULL
            ) ON COMMIT DROP;
            INSERT INTO "Trips" (
                "BookingWindowMinutes",
                "DepartureTime"
            ) VALUES
                (1, TIMESTAMPTZ '2026-08-09 13:01:00+00'),
                (1, TIMESTAMPTZ '2026-08-09 13:00:00+00'),
                (1, TIMESTAMPTZ '2026-08-09 12:59:00+00'),
                (5, TIMESTAMPTZ '2026-08-09 17:00:00+00');
            INSERT INTO "RecurringTrips" ("BookingWindowMinutes") VALUES (2), (5);
            """);

        foreach (var operation in GetUpOperations().OfType<SqlOperation>())
            await context.Database.ExecuteSqlRawAsync(operation.Sql);

        var tripValues = await ReadValuesAsync(
            context.Database.GetDbConnection(),
            "Trips");
        var recurringTripValues = await ReadValuesAsync(
            context.Database.GetDbConnection(),
            "RecurringTrips");
        Assert.Equal(new[] { 60, 60, 60, 300 }, tripValues);
        Assert.Equal(new[] { 120, 300 }, recurringTripValues);
        Assert.Equal(
            1,
            await ReadOpenTripCountAsync(
                context.Database.GetDbConnection()));

        await transaction.RollbackAsync();
    }

    private static IReadOnlyList<MigrationOperation> GetUpOperations()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var migration = new ConvertBookingWindowHoursToMinutes();
        typeof(ConvertBookingWindowHoursToMinutes)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }

    private static async Task<int[]> ReadValuesAsync(
        DbConnection connection,
        string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT array_agg("BookingWindowMinutes" ORDER BY "BookingWindowMinutes")
            FROM "{table}";
            """;
        var result = await command.ExecuteScalarAsync();
        return Assert.IsType<int[]>(result);
    }

    private static async Task<int> ReadOpenTripCountAsync(
        DbConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)::integer
            FROM "Trips"
            WHERE "DepartureTime" +
                (-"BookingWindowMinutes" * INTERVAL '1 minute') >
                TIMESTAMPTZ '2026-08-09 12:00:00+00';
            """;
        var result = await command.ExecuteScalarAsync();
        return Assert.IsType<int>(result);
    }

    private sealed class DatabaseFactAttribute : FactAttribute
    {
        public DatabaseFactAttribute()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable(
                        "PRYDE_RUN_DATABASE_TESTS"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                Skip = "Set PRYDE_RUN_DATABASE_TESTS=true to run database integration tests.";
            }
        }
    }
}
