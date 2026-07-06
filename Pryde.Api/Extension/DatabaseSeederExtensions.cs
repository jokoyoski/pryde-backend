using Pryde.Persistence.Seed;

namespace Pryde.Api.Extensions;

public static class DatabaseSeederExtensions
{
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        await DatabaseSeeder.SeedAsync(app.Services);
    }
}