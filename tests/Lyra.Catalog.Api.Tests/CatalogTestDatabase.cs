namespace Lyra.Catalog.Api.Tests;

using Lyra.Catalog.Api.Data;
using Microsoft.EntityFrameworkCore;

public static class CatalogTestDatabase
{
    public static async Task ResetAsync(string connectionString, CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new CatalogDbContext(options);
        await db.Database.MigrateAsync(ct);
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE user_tracks, tracks, \"OutboxMessage\", \"InboxState\", \"OutboxState\" " +
            "RESTART IDENTITY CASCADE;",
            ct);
    }
}
