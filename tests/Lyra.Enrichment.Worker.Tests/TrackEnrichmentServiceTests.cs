namespace Lyra.Enrichment.Worker.Tests;

using Lyra.AI;
using Lyra.Contracts.AI;
using Lyra.Contracts.Events;
using Lyra.Enrichment.Worker.Models;
using Lyra.Enrichment.Worker.Services.Enrichment;
using Lyra.Enrichment.Worker.Tests.TestDoubles;
using Microsoft.Extensions.Time.Testing;
using Xunit;

public class TrackEnrichmentServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EnrichAsync_CacheHitReturnsEventWithoutDependencyCalls()
    {
        var store = new FakeEnrichmentResultStore
        {
            CachedResult = Cached("cached lyrics", "calm", ["ambient"], 2)
        };
        var service = CreateService(store);

        var result = await service.EnrichAsync(Import(), "session-1", CancellationToken.None);

        Assert.Equal("cached lyrics", result.Lyrics);
        Assert.Equal("calm", result.PrimaryMood);
        Assert.Equal(["ambient"], result.Themes);
        Assert.Equal(2, result.EnergyLevel);
        Assert.Equal(Now, result.EnrichedAt);
        Assert.Equal(1, store.GetCalls);
        Assert.Equal(0, store.LockCalls);
    }

    [Fact]
    public async Task EnrichAsync_CacheMissRunsPipelineCachesAndReleasesLock()
    {
        var store = new FakeEnrichmentResultStore();
        var connectionClient = new FakeUserAiConnectionClient((_, _) =>
            Task.FromResult(Connection()));
        const string fullLyrics = "full plain lyrics that must remain intact";
        var lyricsClient = new FakeLyricsClient((_, _, _, _, _) =>
            Task.FromResult<string?>(fullLyrics));
        var analysisService = new FakeTrackAnalysisService((_, _, _, _) =>
            Task.FromResult(new TrackAnalysis("melancholic", ["night", "loss"], 3)));
        var service = CreateService(
            store,
            connectionClient,
            lyricsClient,
            analysisService);

        var result = await service.EnrichAsync(Import(), "session-1", CancellationToken.None);

        Assert.Equal(fullLyrics, result.Lyrics);
        Assert.Equal("melancholic", result.PrimaryMood);
        Assert.Equal(["night", "loss"], result.Themes);
        Assert.Equal(3, result.EnergyLevel);
        Assert.Equal(1, store.SetCalls);
        Assert.NotNull(store.LastSetResult);
        Assert.Equal(fullLyrics, store.LastSetResult.Lyrics);
        Assert.Equal(SemanticSummaryBuilder.Build(
            "Song Title",
            "Artist",
            "Album",
            "melancholic",
            ["night", "loss"],
            3), result.SemanticSummary);
        Assert.Equal(1, connectionClient.Calls);
        Assert.Equal(1, analysisService.Calls);
        Assert.Equal(fullLyrics, analysisService.LastRequest?.Lyrics);
        Assert.Equal("gpt-test", analysisService.LastConnection?.Model);
        Assert.Equal("session-1", analysisService.LastSessionId);
        Assert.Equal(1, store.LockDisposals);
    }

    [Fact]
    public async Task EnrichAsync_MissingLyricsStillProducesMetadataOnlyEnrichment()
    {
        var store = new FakeEnrichmentResultStore();
        var lyricsClient = new FakeLyricsClient((_, _, _, _, _) =>
            Task.FromResult<string?>(null));
        var analysisService = new FakeTrackAnalysisService((request, _, _, _) =>
        {
            Assert.Null(request.Lyrics);
            return Task.FromResult(new TrackAnalysis("calm", [], 1));
        });
        var service = CreateService(
            store,
            lyricsClient: lyricsClient,
            analysisService: analysisService);

        var result = await service.EnrichAsync(Import(), "session-1", CancellationToken.None);

        Assert.Null(result.Lyrics);
        Assert.Equal(1, store.SetCalls);
    }

    [Fact]
    public async Task EnrichAsync_LockContentionIsRetryableAndCallsNoDependencies()
    {
        var store = new FakeEnrichmentResultStore { LockAvailable = false };
        var connectionClient = new FakeUserAiConnectionClient((_, _) =>
            throw new InvalidOperationException("must not be called"));
        var service = CreateService(store, connectionClient);

        var exception = await Assert.ThrowsAsync<EnrichmentLockUnavailableException>(
            () => service.EnrichAsync(Import(), "session-1", CancellationToken.None));

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), exception.TrackId);
        Assert.Equal(0, connectionClient.Calls);
        Assert.Equal(0, store.SetCalls);
        Assert.Equal(0, store.LockDisposals);
    }

    [Fact]
    public async Task EnrichAsync_AnalysisFailureDoesNotCacheAndReleasesLock()
    {
        var store = new FakeEnrichmentResultStore();
        var analysisService = new FakeTrackAnalysisService((_, _, _, _) =>
            throw new AiProviderException(
                AiProvider.OpenAI,
                statusCode: 503,
                isTransient: true,
                "Provider unavailable.",
                nameof(AiProviderException)));
        var service = CreateService(store, analysisService: analysisService);

        await Assert.ThrowsAsync<AiProviderException>(
            () => service.EnrichAsync(Import(), "session-1", CancellationToken.None));

        Assert.Equal(0, store.SetCalls);
        Assert.Equal(1, store.LockDisposals);
        Assert.Null(store.CachedResult);
    }

    [Fact]
    public async Task EnrichAsync_SecondCacheCheckAfterLockReusesConcurrentResult()
    {
        var cached = Cached("winner lyrics", "energetic", ["dance"], 9);
        var store = new FakeEnrichmentResultStore();
        store.EnqueueGet(null, cached);
        var connectionClient = new FakeUserAiConnectionClient((_, _) =>
            throw new InvalidOperationException("must not be called"));
        var service = CreateService(store, connectionClient);

        var result = await service.EnrichAsync(Import(), "session-1", CancellationToken.None);

        Assert.Equal("winner lyrics", result.Lyrics);
        Assert.Equal(2, store.GetCalls);
        Assert.Equal(1, store.LockCalls);
        Assert.Equal(1, store.LockDisposals);
        Assert.Equal(0, connectionClient.Calls);
        Assert.Equal(0, store.SetCalls);
    }

    private static TrackEnrichmentService CreateService(
        FakeEnrichmentResultStore store,
        FakeUserAiConnectionClient? connectionClient = null,
        FakeLyricsClient? lyricsClient = null,
        FakeTrackAnalysisService? analysisService = null) =>
        new(
            store,
            connectionClient ?? new FakeUserAiConnectionClient((_, _) =>
                Task.FromResult(Connection())),
            lyricsClient ?? new FakeLyricsClient((_, _, _, _, _) =>
                Task.FromResult<string?>("lyrics")),
            analysisService ?? new FakeTrackAnalysisService((_, _, _, _) =>
                Task.FromResult(new TrackAnalysis("calm", ["ambient"], 2))),
            new FakeTimeProvider(Now));

    private static AiConnectionSecret Connection() =>
        new(AiProvider.OpenAI, "gpt-test", "sk-test", null);

    private static TrackEnrichmentResult Cached(
        string? lyrics,
        string mood,
        IReadOnlyList<string> themes,
        int energy) =>
        new(
            lyrics,
            mood,
            themes,
            energy,
            SemanticSummaryBuilder.Build(
                "Song Title",
                "Artist",
                "Album",
                mood,
                themes,
                energy));

    private static SongImportedEvent Import() =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "local-1",
            "Song Title",
            "Artist",
            "Album",
            180,
            Now.AddMinutes(-1));
}
