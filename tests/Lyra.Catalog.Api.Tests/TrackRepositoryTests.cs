namespace Lyra.Catalog.Api.Tests;

using Lyra.Catalog.Api.Data;
using Lyra.Catalog.Api.Models;
using Lyra.Catalog.Api.Repositories;
using Lyra.Catalog.Api.Services.Catalog;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class TrackRepositoryTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture _fixture;

    public TrackRepositoryTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<CatalogDbContext> CreateDbContextAsync()
    {
        await CatalogTestDatabase.ResetAsync(_fixture.ConnectionString);

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        return new CatalogDbContext(options);
    }

    private static Track CreateTrack(string title, string artist, int durationSeconds) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Artist = artist,
        Album = null,
        DurationSeconds = durationSeconds,
        TrackFingerprint = FingerprintGenerator.Compute(title, artist, durationSeconds)
    };

    [Fact]
    public async Task UpsertTracksAsync_NewTrack_InsertsOneGlobalRowAndReturnsStableId()
    {
        await using var db = await CreateDbContextAsync();
        var repository = new TrackRepository(db);
        var track = CreateTrack("Bohemian Rhapsody", "Queen", 355);

        var firstMap = await repository.UpsertTracksAsync([track], default);
        await repository.SaveChangesAsync(default);

        Assert.Single(firstMap);
        Assert.Equal(1, await db.Tracks.CountAsync());

        var duplicate = CreateTrack("bohemian  rhapsody", "QUEEN", 355);
        var secondMap = await repository.UpsertTracksAsync([duplicate], default);
        await repository.SaveChangesAsync(default);

        Assert.Equal(firstMap[track.TrackFingerprint], secondMap[duplicate.TrackFingerprint]);
        Assert.Equal(1, await db.Tracks.CountAsync());
    }

    [Fact]
    public async Task GetLinkedFingerprintsAsync_ReturnsOnlyRequestedUsersLinks()
    {
        await using var db = await CreateDbContextAsync();
        var repository = new TrackRepository(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var bohemian = CreateTrack("Bohemian Rhapsody", "Queen", 355);
        var sultans = CreateTrack("Sultans of Swing", "Dire Straits", 348);

        var map = await repository.UpsertTracksAsync([bohemian, sultans], default);
        await repository.LinkUserTracksAsync(
            userA,
            [(map[bohemian.TrackFingerprint], "local-bohemian", DateTimeOffset.UtcNow)],
            default);
        await repository.SaveChangesAsync(default);

        var fingerprints = new[] { bohemian.TrackFingerprint, sultans.TrackFingerprint };

        var linkedA = await repository.GetLinkedFingerprintsAsync(userA, fingerprints, default);
        var linkedB = await repository.GetLinkedFingerprintsAsync(userB, fingerprints, default);

        Assert.Contains(bohemian.TrackFingerprint, linkedA);
        Assert.DoesNotContain(sultans.TrackFingerprint, linkedA);
        Assert.Empty(linkedB);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsNewestFirstWithTotalCount()
    {
        await using var db = await CreateDbContextAsync();
        var repository = new TrackRepository(db);
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var oldest = CreateTrack("Oldest", "Artist", 100);
        var newest = CreateTrack("Newest", "Artist", 100);
        var middle = CreateTrack("Middle", "Artist", 100);

        var map = await repository.UpsertTracksAsync([oldest, middle, newest], default);
        await repository.LinkUserTracksAsync(
            userId,
            [
                (map[oldest.TrackFingerprint], "local-oldest", now.AddMinutes(-2)),
                (map[middle.TrackFingerprint], "local-middle", now.AddMinutes(-1)),
                (map[newest.TrackFingerprint], "local-newest", now)
            ],
            default);
        await repository.SaveChangesAsync(default);

        var (firstPage, totalCount) = await repository.GetPagedAsync(userId, 1, 2, default);
        var (secondPage, secondTotal) = await repository.GetPagedAsync(userId, 2, 2, default);

        Assert.Equal(3, totalCount);
        Assert.Equal(3, secondTotal);
        Assert.Equal(["Newest", "Middle"], firstPage.Select(item => item.Title));
        Assert.Single(secondPage);
        Assert.Equal("Oldest", secondPage[0].Title);
        Assert.Equal("local-newest", firstPage[0].LocalTrackId);
    }

    [Fact]
    public async Task SearchFuzzyAsync_MatchesTyposAndStaysScopedToTheUser()
    {
        await using var db = await CreateDbContextAsync();
        var repository = new TrackRepository(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var bohemian = CreateTrack("Bohemian Rhapsody", "Queen", 355);
        var sultans = CreateTrack("Sultans of Swing", "Dire Straits", 348);

        var map = await repository.UpsertTracksAsync([bohemian, sultans], default);
        await repository.LinkUserTracksAsync(
            userA,
            [
                (map[bohemian.TrackFingerprint], "local-bohemian", DateTimeOffset.UtcNow),
                (map[sultans.TrackFingerprint], "local-sultans", DateTimeOffset.UtcNow)
            ],
            default);
        await repository.LinkUserTracksAsync(
            userB,
            [(map[bohemian.TrackFingerprint], "local-bohemian-b", DateTimeOffset.UtcNow)],
            default);
        await repository.SaveChangesAsync(default);

        var aResults = await repository.SearchFuzzyAsync(userA, "bohemain rhapsody", 0.2, 50, default);
        var bResults = await repository.SearchFuzzyAsync(userB, "sultans of swing", 0.2, 50, default);
        var noResults = await repository.SearchFuzzyAsync(userA, "zzzzzz", 0.5, 50, default);

        Assert.Contains(aResults, item => item.Title == "Bohemian Rhapsody");
        Assert.DoesNotContain(aResults, item => item.Title == "Sultans of Swing");
        Assert.Empty(bResults);
        Assert.Empty(noResults);
    }
}
