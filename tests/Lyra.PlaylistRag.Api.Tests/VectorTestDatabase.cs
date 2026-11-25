namespace Lyra.PlaylistRag.Api.Tests;

using Lyra.PlaylistRag.Api.Data;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

public static class VectorTestDatabase
{
    public static DbContextOptions<VectorDbContext> Options(string connectionString) =>
        new DbContextOptionsBuilder<VectorDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;

    public static async Task ResetAsync(string connectionString, CancellationToken ct = default)
    {
        await using var db = new VectorDbContext(Options(connectionString));
        await db.Database.MigrateAsync(ct);
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE user_tracks, track_embeddings RESTART IDENTITY CASCADE;",
            ct);
    }
}
