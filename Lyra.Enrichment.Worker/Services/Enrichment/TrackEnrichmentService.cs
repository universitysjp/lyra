namespace Lyra.Enrichment.Worker.Services.Enrichment;

using Lyra.Contracts.Events;
using Lyra.Enrichment.Worker.Models;
using Lyra.Enrichment.Worker.Services.AiConnections;
using Lyra.Enrichment.Worker.Services.Caching;
using Lyra.Enrichment.Worker.Services.Lyrics;
using Microsoft.Extensions.Logging;

public sealed class TrackEnrichmentService(
    IEnrichmentResultStore resultStore,
    IUserAiConnectionClient connectionClient,
    ILyricsClient lyricsClient,
    ITrackAnalysisService analysisService,
    TimeProvider? timeProvider = null,
    ILogger<TrackEnrichmentService>? logger = null,
    ILyricsFetchedPublisher? lyricsPublisher = null) : ITrackEnrichmentService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ILogger? _logger = logger;

    public async Task<SongEnrichedEvent> EnrichAsync(
        SongImportedEvent request,
        string sessionId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("A stable enrichment session ID is required.", nameof(sessionId));
        }

        var cached = await resultStore.GetAsync(request.TrackId, ct);
        if (cached is not null)
        {
            return await UseCachedAsync(request, cached, ct);
        }

        var enrichmentLock = await resultStore.TryAcquireLockAsync(request.TrackId, ct)
            ?? throw new EnrichmentLockUnavailableException(request.TrackId);

        await using (enrichmentLock)
        {
            cached = await resultStore.GetAsync(request.TrackId, ct);
            if (cached is not null)
            {
                return await UseCachedAsync(request, cached, ct);
            }

            var lyrics = await lyricsClient.GetLyricsAsync(
                request.Title,
                request.Artist,
                request.Album,
                request.DurationSeconds,
                ct);

            // Persist lyrics before touching the user's AI connection. Publishing through
            // the bus (not the consumer outbox) means a later gRPC or provider failure
            // cannot roll this back.
            await PublishLyricsAsync(request, lyrics, ct);

            var connection = await connectionClient.GetRequiredAsync(request.UserId, ct);

            var analysis = await analysisService.AnalyzeAsync(
                new TrackAnalysisRequest(
                    request.Title,
                    request.Artist,
                    request.Album,
                    request.DurationSeconds,
                    lyrics.PlainLyrics),
                connection,
                sessionId,
                ct);

            var summary = SemanticSummaryBuilder.Build(
                request.Title,
                request.Artist,
                request.Album,
                analysis.PrimaryMood,
                analysis.Themes,
                analysis.EnergyLevel);

            var result = new TrackEnrichmentResult(
                lyrics.PlainLyrics,
                lyrics.SyncedLyrics,
                analysis.PrimaryMood,
                analysis.Themes,
                analysis.EnergyLevel,
                summary,
                lyrics.Metadata);

            await resultStore.SetAsync(request.TrackId, result, ct);
            _logger?.LogInformation(
                "Enriched track {TrackId} for user {UserId}.",
                request.TrackId,
                request.UserId);

            return CreateEvent(request, result);
        }
    }

    private async Task<SongEnrichedEvent> UseCachedAsync(
        SongImportedEvent request,
        TrackEnrichmentResult cached,
        CancellationToken ct)
    {
        var hasLyrics = cached.Lyrics is not null || cached.SyncedLyrics is not null;

        if (hasLyrics && cached.ResolvedMetadata is not null)
        {
            _logger?.LogDebug(
                "Using cached enrichment for track {TrackId} while processing user {UserId}.",
                request.TrackId,
                request.UserId);
            return CreateEvent(request, cached);
        }

        // Older cache entries were created before lyrics or canonical metadata existed.
        // Re-check LRCLIB without repeating the paid AI call, then upgrade the cache.
        var freshLyrics = await lyricsClient.GetLyricsAsync(
            request.Title,
            request.Artist,
            request.Album,
            request.DurationSeconds,
            ct);

        if (!freshLyrics.HasLyrics)
        {
            _logger?.LogDebug(
                "Cached enrichment for track {TrackId} still has no lyrics to merge.",
                request.TrackId);
            return CreateEvent(request, cached);
        }

        var merged = cached with
        {
            Lyrics = freshLyrics.PlainLyrics ?? cached.Lyrics,
            SyncedLyrics = freshLyrics.SyncedLyrics ?? cached.SyncedLyrics,
            ResolvedMetadata = freshLyrics.Metadata ?? cached.ResolvedMetadata
        };

        await PublishLyricsAsync(
            request,
            new LyricsResult(merged.Lyrics, merged.SyncedLyrics, merged.ResolvedMetadata),
            ct);
        await resultStore.SetAsync(request.TrackId, merged, ct);

        return CreateEvent(request, merged);
    }

    private Task PublishLyricsAsync(
        SongImportedEvent request,
        LyricsResult lyrics,
        CancellationToken ct)
    {
        if (!lyrics.HasLyrics || lyricsPublisher is null)
        {
            return Task.CompletedTask;
        }

        return lyricsPublisher.PublishAsync(
            new SongLyricsFetchedEvent(
                request.TrackId,
                request.UserId,
                lyrics.PlainLyrics,
                lyrics.SyncedLyrics,
                _timeProvider.GetUtcNow(),
                lyrics.Metadata),
            ct);
    }

    private SongEnrichedEvent CreateEvent(
        SongImportedEvent request,
        TrackEnrichmentResult result) =>
        new(
            request.TrackId,
            request.UserId,
            result.Lyrics,
            result.SyncedLyrics,
            result.PrimaryMood,
            result.Themes,
            result.EnergyLevel,
            result.SemanticSummary,
            _timeProvider.GetUtcNow(),
            result.ResolvedMetadata);
}
