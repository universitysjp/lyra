namespace Lyra.Catalog.Api.Tests;

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

public class TrackLyricsServiceTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture _fixture;

    public TrackLyricsServiceTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ApplyAsync_StoresLyricsWithoutMarkingEnriched_AndInvalidatesLinkedUsers()
    {
        var (db, trackId, users) = await CreateLibraryAsync(2);
        await using var _ = db;
        var cache = CreateCache();
        await SeedCacheVersionsAsync(cache, users);
        var service = CreateService(db, new LibraryCache(cache));

        var applied = await service.ApplyAsync(
            Event(trackId, users[0], "plain lyrics", "synced lyrics"),
            CancellationToken.None);

        Assert.True(applied);
        db.ChangeTracker.Clear();
        var track = await db.Tracks.AsNoTracking().SingleAsync(item => item.Id == trackId);
        Assert.False(track.IsEnriched);
        Assert.Equal("plain lyrics", track.Lyrics);
        Assert.Equal("synced lyrics", track.SyncedLyrics);
        Assert.Null(track.PrimaryMood);
        Assert.Equal("Resolved Title", track.ResolvedTitle);
        Assert.Equal("Resolved Artist", track.ResolvedArtist);
        Assert.Equal("Resolved Album", track.ResolvedAlbum);
        Assert.Equal(214, track.ResolvedDurationSeconds);
        Assert.False(track.ResolvedInstrumental);

        foreach (var userId in users)
        {
            var version = await cache.GetStringAsync(VersionKey(userId));
            Assert.NotEqual("old", version);
        }
    }

    [Fact]
    public async Task ApplyAsync_PartialEventPreservesExistingLyrics()
    {
        var (db, trackId, users) = await CreateLibraryAsync(1);
        await using var _ = db;
        var service = CreateService(db, new LibraryCache(CreateCache()));

        await service.ApplyAsync(
            Event(trackId, users[0], "plain lyrics", "synced lyrics"),
            CancellationToken.None);
        var applied = await service.ApplyAsync(
            Event(trackId, users[0], "updated plain lyrics", null),
            CancellationToken.None);

        Assert.True(applied);
        db.ChangeTracker.Clear();
        var track = await db.Tracks.AsNoTracking().SingleAsync(item => item.Id == trackId);
        Assert.Equal("updated plain lyrics", track.Lyrics);
        Assert.Equal("synced lyrics", track.SyncedLyrics);
    }

    [Fact]
    public async Task ApplyAsync_UnknownTrackThrowsNotFound()
    {
        await CatalogTestDatabase.ResetAsync(_fixture.ConnectionString);
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        await using var db = new CatalogDbContext(options);
        var service = CreateService(db, new LibraryCache(CreateCache()));

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.ApplyAsync(
                Event(Guid.NewGuid(), Guid.NewGuid(), "plain", "synced"),
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

    private static TrackLyricsService CreateService(CatalogDbContext db, ILibraryCache cache) =>
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

    private static SongLyricsFetchedEvent Event(
        Guid trackId,
        Guid userId,
        string? lyrics,
        string? syncedLyrics) =>
        new(
            trackId,
            userId,
            lyrics,
            syncedLyrics,
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
