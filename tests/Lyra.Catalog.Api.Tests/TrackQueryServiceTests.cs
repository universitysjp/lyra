namespace Lyra.Catalog.Api.Tests;

using Lyra.Catalog.Api.Configuration;
using Lyra.Catalog.Api.Data;
using Lyra.Catalog.Api.Exceptions;
using Lyra.Catalog.Api.Models;
using Lyra.Catalog.Api.Repositories;
using Lyra.Catalog.Api.Services.Caching;
using Lyra.Catalog.Api.Services.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

public class TrackQueryServiceTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture _fixture;

    public TrackQueryServiceTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(CatalogDbContext Db, TrackQueryService Service, LibraryCache Cache)> CreateAsync()
    {
        await CatalogTestDatabase.ResetAsync(_fixture.ConnectionString);

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        var db = new CatalogDbContext(options);
        var repository = new TrackRepository(db);
        var catalogOptions = Options.Create(new CatalogOptions());

        var cache = new LibraryCache(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            catalogOptions);

        var service = new TrackQueryService(
            repository,
            cache,
            catalogOptions,
            Options.Create(new AppStringOptions()));

        return (db, service, cache);
    }

    private static async Task SeedLibraryAsync(
        CatalogDbContext db, ITrackRepository repository, Guid userId)
    {
        var tracks = new[]
        {
            CreateTrack("Bohemian Rhapsody", "Queen", 355),
            CreateTrack("Sultans of Swing", "Dire Straits", 348)
        };

        var map = await repository.UpsertTracksAsync(tracks, default);
        var now = DateTimeOffset.UtcNow;

        await repository.LinkUserTracksAsync(
            userId,
            tracks.Select((track, index) =>
                (map[track.TrackFingerprint], $"local-{index}", now.AddMinutes(index))).ToList(),
            default);
        await repository.SaveChangesAsync(default);
    }

    private static Track CreateTrack(string title, string artist, int durationSeconds) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Artist = artist,
        DurationSeconds = durationSeconds,
        TrackFingerprint = FingerprintGenerator.Compute(title, artist, durationSeconds)
    };

    [Fact]
    public async Task GetLibraryAsync_ServesFromCache_UntilInvalidated()
    {
        var (db, service, cache) = await CreateAsync();
        await using var _ = db;
        var userId = Guid.NewGuid();
        var repository = new TrackRepository(db);

        await SeedLibraryAsync(db, repository, userId);

        var first = await service.GetLibraryAsync(userId, 1, 50, default);
        Assert.Equal(2, first.TotalCount);

        await db.UserTracks
            .Where(link => link.UserId == userId)
            .ExecuteDeleteAsync();

        var cached = await service.GetLibraryAsync(userId, 1, 50, default);
        Assert.Equal(2, cached.Items.Count);

        await cache.InvalidateAsync(userId, default);

        var afterInvalidation = await service.GetLibraryAsync(userId, 1, 50, default);
        Assert.Empty(afterInvalidation.Items);
        Assert.Equal(0, afterInvalidation.TotalCount);
    }

    [Fact]
    public async Task GetLibraryAsync_ClampsPageAndPageSize()
    {
        var (db, service, _) = await CreateAsync();
        await using var _ = db;
        var userId = Guid.NewGuid();
        var repository = new TrackRepository(db);

        await SeedLibraryAsync(db, repository, userId);

        var defaulted = await service.GetLibraryAsync(userId, 0, 0, default);
        var capped = await service.GetLibraryAsync(userId, 1, 9999, default);

        Assert.Equal(1, defaulted.Page);
        Assert.Equal(50, defaulted.PageSize);
        Assert.Equal(1, capped.Page);
        Assert.Equal(200, capped.PageSize);
    }

    [Fact]
    public async Task SearchAsync_BlankQuery_ThrowsValidationException()
    {
        var (db, service, _) = await CreateAsync();
        await using var _ = db;

        await Assert.ThrowsAsync<ValidationException>(
            () => service.SearchAsync(Guid.NewGuid(), "   ", default));
    }
}
