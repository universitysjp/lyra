namespace Lyra.Catalog.Api.Tests;

using System.Text.Json;
using Lyra.Catalog.Api.Configuration;
using Lyra.Catalog.Api.Data;
using Lyra.Catalog.Api.Exceptions;
using Lyra.Catalog.Api.Models;
using Lyra.Catalog.Api.Repositories;
using Lyra.Catalog.Api.Services.Caching;
using Lyra.Catalog.Api.Services.Catalog;
using Lyra.Contracts.Events;
using Lyra.Contracts.Music;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

public class TrackEnrichmentServiceTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture _fixture;

    public TrackEnrichmentServiceTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ApplyAsync_FirstEventUpdatesTrackAndInvalidatesAllLinkedUsers()
    {
        var (db, trackId, users) = await CreateLibraryAsync(2);
        await using var _ = db;
        var distributedCache = CreateCache();
        var libraryCache = new LibraryCache(distributedCache);
        await SeedCacheVersionsAsync(distributedCache, users);
        var service = CreateService(db, libraryCache);

        var applied = await service.ApplyAsync(Event(trackId, users[0], 4), CancellationToken.None);

        Assert.True(applied);
        db.ChangeTracker.Clear();
        var track = await db.Tracks.AsNoTracking().SingleAsync(item => item.Id == trackId);
        Assert.True(track.IsEnriched);
        Assert.Equal("lyrics", track.Lyrics);
        Assert.Equal("synced lyrics", track.SyncedLyrics);
        Assert.Equal("Resolved Title", track.ResolvedTitle);
        Assert.Equal("Resolved Artist", track.ResolvedArtist);
        Assert.Equal("Resolved Album", track.ResolvedAlbum);
        Assert.Equal(214, track.ResolvedDurationSeconds);
        Assert.False(track.ResolvedInstrumental);
        Assert.Equal("melancholic", track.PrimaryMood);
        var themes = JsonSerializer.Deserialize<string[]>(track.Themes!);
        Assert.NotNull(themes);
        Assert.Equal(["night", "loss"], themes);
        Assert.Equal(4, track.EnergyLevel);
        Assert.Equal("summary", track.SemanticSummary);

        foreach (var userId in users)
        {
            var version = await distributedCache.GetStringAsync(VersionKey(userId));
            Assert.NotNull(version);
            Assert.NotEqual("old", version);
        }
    }

    [Fact]
    public async Task ApplyAsync_DuplicateDoesNotOverwriteFirstEnrichment()
    {
        var (db, trackId, users) = await CreateLibraryAsync(1);
        await using var _ = db;
        var service = CreateService(db, new LibraryCache(CreateCache()));

        await service.ApplyAsync(Event(trackId, users[0], 3), CancellationToken.None);
        db.ChangeTracker.Clear();
        var appliedAgain = await service.ApplyAsync(
            Event(trackId, users[0], 10) with
            {
                PrimaryMood = "energetic",
                Themes = ["dance"],
                SemanticSummary = "replacement"
            },
            CancellationToken.None);

        Assert.False(appliedAgain);
        db.ChangeTracker.Clear();
        var track = await db.Tracks.AsNoTracking().SingleAsync(item => item.Id == trackId);
        Assert.Equal("melancholic", track.PrimaryMood);
        var themes = JsonSerializer.Deserialize<string[]>(track.Themes!);
        Assert.NotNull(themes);
        Assert.Equal(["night", "loss"], themes);
        Assert.Equal(3, track.EnergyLevel);
        Assert.Equal("summary", track.SemanticSummary);
    }

    [Fact]
    public async Task ApplyAsync_UnknownTrackThrowsNotFound()
    {
        await CatalogTestDatabase.ResetAsync(_fixture.ConnectionString);
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        await using var db = new CatalogDbContext(options);
        var strings = new AppStringOptions();
        strings.Errors.TrackNotFound = "Track missing.";
        var service = new TrackEnrichmentService(
            new TrackRepository(db),
            new LibraryCache(CreateCache()),
            Options.Create(strings));

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.ApplyAsync(Event(Guid.NewGuid(), Guid.NewGuid(), 5), CancellationToken.None));
    }

    [Theory]
    [InlineData("", 5)]
    [InlineData("mood", 0)]
    [InlineData("mood", 11)]
    public async Task ApplyAsync_InvalidPayloadIsRejected(string mood, int energy)
    {
        await CatalogTestDatabase.ResetAsync(_fixture.ConnectionString);
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        await using var db = new CatalogDbContext(options);
        var service = CreateService(db, new LibraryCache(CreateCache()));

        await Assert.ThrowsAsync<InvalidEnrichmentException>(
            () => service.ApplyAsync(
                Event(Guid.NewGuid(), Guid.NewGuid(), energy) with
                {
                    PrimaryMood = mood
                },
                CancellationToken.None));
    }

    private async Task<(CatalogDbContext Db, Guid TrackId, Guid[] Users)> CreateLibraryAsync(
        int userCount)
    {
        await CatalogTestDatabase.ResetAsync(_fixture.ConnectionString);
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        var db = new CatalogDbContext(options);
        var repository = new TrackRepository(db);
        var track = new Track
        {
            Id = Guid.NewGuid(),
            Title = "Song",
            Artist = "Artist",
            Album = "Album",
            DurationSeconds = 180,
            TrackFingerprint = FingerprintGenerator.Compute("Song", "Artist", 180)
        };
        var ids = await repository.UpsertTracksAsync([track], CancellationToken.None);
        var users = Enumerable.Range(0, userCount).Select(_ => Guid.NewGuid()).ToArray();
        var now = DateTimeOffset.UtcNow;
        await repository.LinkUserTracksAsync(
            users[0],
            [(ids[track.TrackFingerprint], "local-0", now)],
            CancellationToken.None);

        for (var index = 1; index < users.Length; index++)
        {
            await repository.LinkUserTracksAsync(
                users[index],
                [(ids[track.TrackFingerprint], $"local-{index}", now)],
                CancellationToken.None);
        }

        await repository.SaveChangesAsync(CancellationToken.None);

        return (db, ids[track.TrackFingerprint], users);
    }

    private static TrackEnrichmentService CreateService(
        CatalogDbContext db,
        ILibraryCache cache) =>
        new(
            new TrackRepository(db),
            cache,
            Options.Create(new AppStringOptions
            {
                Errors = new AppStringOptions.ErrorStrings
                {
                    TrackNotFound = "Track missing."
                }
            }));

    private static SongEnrichedEvent Event(Guid trackId, Guid userId, int energy) =>
        new(
            trackId,
            userId,
            "lyrics",
            "synced lyrics",
            "melancholic",
            ["night", "loss"],
            energy,
            "summary",
            DateTimeOffset.UtcNow,
            new ResolvedTrackMetadata(
                "Resolved Title",
                "Resolved Artist",
                "Resolved Album",
                214,
                Instrumental: false));

    private static async Task SeedCacheVersionsAsync(
        IDistributedCache cache,
        IEnumerable<Guid> users)
    {
        foreach (var userId in users)
        {
            await cache.SetStringAsync(VersionKey(userId), "old");
        }
    }

    private static MemoryDistributedCache CreateCache() =>
        new(Options.Create(new MemoryDistributedCacheOptions()));

    private static string VersionKey(Guid userId) => $"catalog:tracks:{userId}:ver";
}
