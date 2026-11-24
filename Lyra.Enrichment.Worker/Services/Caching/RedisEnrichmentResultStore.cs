namespace Lyra.Enrichment.Worker.Services.Caching;

using System.Text.Json;
using Lyra.Enrichment.Worker.Configuration;
using Lyra.Enrichment.Worker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

public sealed class RedisEnrichmentResultStore(
    IConnectionMultiplexer redis,
    IOptions<EnrichmentOptions>? options = null,
    ILogger<RedisEnrichmentResultStore>? logger = null) : IEnrichmentResultStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly EnrichmentOptions _options = options?.Value ?? new EnrichmentOptions();
    private readonly ILogger? _logger = logger;

    public async Task<TrackEnrichmentResult?> GetAsync(Guid trackId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var value = await redis.GetDatabase()
            .StringGetAsync(ResultKey(trackId))
            .WaitAsync(ct);

        if (!value.HasValue)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TrackEnrichmentResult>(
                value.ToString(),
                SerializerOptions);
        }
        catch (JsonException exception)
        {
            _logger?.LogWarning(
                exception,
                "Ignoring malformed cached enrichment result for track {TrackId}.",
                trackId);
            return null;
        }
    }

    public async Task SetAsync(
        Guid trackId,
        TrackEnrichmentResult result,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateOptions();
        ct.ThrowIfCancellationRequested();

        var json = JsonSerializer.Serialize(result, SerializerOptions);
        await redis.GetDatabase()
            .StringSetAsync(ResultKey(trackId), json, _options.ResultCacheTtl)
            .WaitAsync(ct);
    }

    public async Task<ITrackEnrichmentLock?> TryAcquireLockAsync(
        Guid trackId,
        CancellationToken ct)
    {
        ValidateOptions();
        ct.ThrowIfCancellationRequested();

        var ownerToken = Guid.NewGuid().ToString("N");
        var acquired = await redis.GetDatabase()
            .LockTakeAsync(LockKey(trackId), ownerToken, _options.LockExpiry)
            .WaitAsync(ct);

        return acquired
            ? new RedisTrackEnrichmentLock(
                redis.GetDatabase(),
                LockKey(trackId),
                ownerToken,
                _logger)
            : null;
    }

    internal static string ResultKey(Guid trackId) => $"enrichment:tracks:{trackId}:v1";

    internal static string LockKey(Guid trackId) => $"enrichment:locks:{trackId}:v1";

    private void ValidateOptions()
    {
        if (_options.ResultCacheTtl <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Enrichment result cache TTL must be positive.");
        }

        if (_options.LockExpiry <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Enrichment lock expiry must be positive.");
        }
    }

    private sealed class RedisTrackEnrichmentLock(
        IDatabase database,
        RedisKey key,
        string ownerToken,
        ILogger? logger) : ITrackEnrichmentLock
    {
        private int _released;

        public string OwnerToken { get; } = ownerToken;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
            {
                return;
            }

            try
            {
                await database.LockReleaseAsync(key, OwnerToken);
            }
            catch (RedisException exception)
            {
                logger?.LogWarning(
                    exception,
                    "Failed to release enrichment lock {LockKey}; it will expire automatically.",
                    key);
            }
        }
    }
}
