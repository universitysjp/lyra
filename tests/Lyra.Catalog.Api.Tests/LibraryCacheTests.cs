namespace Lyra.Catalog.Api.Tests;

using Lyra.Catalog.Api.Configuration;
using Lyra.Catalog.Api.DTOs.Responses;
using Lyra.Catalog.Api.Repositories;
using Lyra.Catalog.Api.Services.Caching;
using Lyra.Catalog.Api.Services.Catalog;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

public class LibraryCacheTests
{
    [Fact]
    public async Task InvalidateAsync_ReplacesVersionWithOpaqueToken()
    {
        var distributedCache = CreateCache();
        var cache = new LibraryCache(distributedCache);
        var userId = Guid.NewGuid();

        await cache.InvalidateAsync(userId, CancellationToken.None);
        var first = await distributedCache.GetStringAsync(VersionKey(userId));
        await cache.InvalidateAsync(userId, CancellationToken.None);
        var second = await distributedCache.GetStringAsync(VersionKey(userId));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
        Assert.False(long.TryParse(first, out _));
        Assert.False(long.TryParse(second, out _));
    }

    [Fact]
    public async Task DelayedCacheFill_ReusesLookupVersionAfterConcurrentInvalidation()
    {
        var userId = Guid.NewGuid();
        var cache = new RecordingLibraryCache("version-a");
        var repository = new DelayedRepository(async () =>
            await cache.InvalidateAsync(userId, CancellationToken.None));
        var service = new TrackQueryService(
            repository,
            cache,
            Options.Create(new CatalogOptions()),
            Options.Create(new AppStringOptions()));

        var response = await service.GetLibraryAsync(userId, 1, 50, CancellationToken.None);

        Assert.Equal("version-a", cache.SetVersion);
        Assert.Same(response, cache.SetResponse);
        Assert.Equal(1, cache.InvalidationCount);
    }

    private static MemoryDistributedCache CreateCache() =>
        new(Options.Create(new MemoryDistributedCacheOptions()));

    private static string VersionKey(Guid userId) => $"catalog:tracks:{userId}:ver";

    private sealed class RecordingLibraryCache(string version) : ILibraryCache
    {
        public string? SetVersion { get; private set; }

        public PagedResponse<TrackResponse>? SetResponse { get; private set; }

        public int InvalidationCount { get; private set; }

        public Task<LibraryCacheLookup> GetLibraryAsync(
            Guid userId, int page, int pageSize, CancellationToken ct) =>
            Task.FromResult(new LibraryCacheLookup(version, null));

        public Task SetLibraryAsync(
            Guid userId,
            string setVersion,
            PagedResponse<TrackResponse> response,
            CancellationToken ct)
        {
            SetVersion = setVersion;
            SetResponse = response;
            return Task.CompletedTask;
        }

        public Task InvalidateAsync(Guid userId, CancellationToken ct)
        {
            InvalidationCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class DelayedRepository(Func<Task> duringQuery) : ITrackRepository
    {
        public async Task<(IReadOnlyList<TrackResponse> Items, int TotalCount)> GetPagedAsync(
            Guid userId, int page, int pageSize, CancellationToken ct)
        {
            await duringQuery();
            return ([], 0);
        }

        public Task<Dictionary<string, Guid>> UpsertTracksAsync(
            IReadOnlyCollection<Lyra.Catalog.Api.Models.Track> tracks,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<HashSet<string>> GetLinkedFingerprintsAsync(
            Guid userId, IReadOnlyCollection<string> fingerprints, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task LinkUserTracksAsync(
            Guid userId,
            IReadOnlyCollection<(Guid TrackId, string LocalTrackId, DateTimeOffset ImportedAt)> links,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TrackResponse>> SearchFuzzyAsync(
            Guid userId, string query, double threshold, int limit, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<TrackLyricsResponse?> GetLyricsAsync(
            Guid userId, Guid trackId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Lyra.Catalog.Api.Repositories.TrackLyricsApplyResult?> ApplyLyricsAsync(
            Guid trackId,
            string? lyrics,
            string? syncedLyrics,
            Lyra.Contracts.Music.ResolvedTrackMetadata? resolvedMetadata,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Lyra.Catalog.Api.Repositories.TrackEnrichmentApplyResult?> ApplyEnrichmentAsync(
            Guid trackId,
            Lyra.Catalog.Api.Models.TrackEnrichmentContent content,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
