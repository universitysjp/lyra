namespace Lyra.Enrichment.Worker.Tests;

using Lyra.Contracts.Music;
using Lyra.Enrichment.Worker.Configuration;
using Lyra.Enrichment.Worker.Models;
using Lyra.Enrichment.Worker.Services.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

public class RedisEnrichmentResultStoreTests : IClassFixture<RedisTestFixture>
{
    private readonly RedisTestFixture _fixture;

    public RedisEnrichmentResultStoreTests(RedisTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SetAndGet_RoundTripsUserIndependentResultWithTtl()
    {
        var trackId = Guid.NewGuid();
        var store = CreateStore(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        var result = new TrackEnrichmentResult(
            "some lyrics",
            "some synced lyrics",
            "melancholic",
            ["dreamy", "night", "Dreamy"],
            4,
            "Slow dreamy track with a melancholic mood.",
            new ResolvedTrackMetadata("Resolved", "Artist", "Album", 214, false));

        await store.SetAsync(trackId, result, CancellationToken.None);
        var loaded = await store.GetAsync(trackId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(result.Lyrics, loaded.Lyrics);
        Assert.Equal(result.SyncedLyrics, loaded.SyncedLyrics);
        Assert.Equal(result.PrimaryMood, loaded.PrimaryMood);
        Assert.Equal(result.Themes, loaded.Themes);
        Assert.Equal(result.EnergyLevel, loaded.EnergyLevel);
        Assert.Equal(result.SemanticSummary, loaded.SemanticSummary);
        Assert.Equal(result.ResolvedMetadata, loaded.ResolvedMetadata);

        var database = _fixture.Redis.GetDatabase();
        var raw = await database.StringGetAsync(
            RedisEnrichmentResultStore.ResultKey(trackId));
        Assert.True(raw.HasValue);
        Assert.DoesNotContain("user", raw.ToString(), StringComparison.OrdinalIgnoreCase);

        var ttl = await database.KeyTimeToLiveAsync(
            RedisEnrichmentResultStore.ResultKey(trackId));
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value.TotalSeconds, 50, 60);
    }

    [Fact]
    public async Task Get_MalformedPayloadReturnsNull()
    {
        var trackId = Guid.NewGuid();
        var store = CreateStore(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        await _fixture.Redis.GetDatabase().StringSetAsync(
            RedisEnrichmentResultStore.ResultKey(trackId),
            "{not-json");

        var loaded = await store.GetAsync(trackId, CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task Lock_IsExclusiveAndOnlyOwnerCanRelease()
    {
        var trackId = Guid.NewGuid();
        var otherTrackId = Guid.NewGuid();
        var store = CreateStore(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        var first = await store.TryAcquireLockAsync(trackId, CancellationToken.None);
        Assert.NotNull(first);
        Assert.False(string.IsNullOrWhiteSpace(first.OwnerToken));

        var blocked = await store.TryAcquireLockAsync(trackId, CancellationToken.None);
        Assert.Null(blocked);

        var otherTrackLock = await store.TryAcquireLockAsync(
            otherTrackId,
            CancellationToken.None);
        Assert.NotNull(otherTrackLock);

        var database = _fixture.Redis.GetDatabase();
        var wrongOwnerReleased = await database.LockReleaseAsync(
            RedisEnrichmentResultStore.LockKey(trackId),
            "wrong-owner");
        Assert.False(wrongOwnerReleased);
        Assert.Null(await store.TryAcquireLockAsync(trackId, CancellationToken.None));

        await first.DisposeAsync();
        await first.DisposeAsync();

        var reacquired = await store.TryAcquireLockAsync(trackId, CancellationToken.None);
        Assert.NotNull(reacquired);

        await reacquired.DisposeAsync();
        await otherTrackLock.DisposeAsync();
    }

    [Fact]
    public async Task Lock_ExpiresAfterConfiguredLease()
    {
        var trackId = Guid.NewGuid();
        var store = CreateStore(
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMilliseconds(250));

        var first = await store.TryAcquireLockAsync(trackId, CancellationToken.None);
        Assert.NotNull(first);

        await Task.Delay(TimeSpan.FromMilliseconds(400));

        var second = await store.TryAcquireLockAsync(trackId, CancellationToken.None);
        Assert.NotNull(second);

        await second.DisposeAsync();
        await first.DisposeAsync();
    }

    private RedisEnrichmentResultStore CreateStore(
        TimeSpan cacheTtl,
        TimeSpan lockExpiry) =>
        new(
            _fixture.Redis,
            Options.Create(new EnrichmentOptions
            {
                ResultCacheTtl = cacheTtl,
                LockExpiry = lockExpiry
            }),
            NullLogger<RedisEnrichmentResultStore>.Instance);
}
