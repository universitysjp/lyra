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
    ILogger<TrackEnrichmentService>? logger = null) : ITrackEnrichmentService
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
            _logger?.LogDebug(
                "Using cached enrichment for track {TrackId} while processing user {UserId}.",
                request.TrackId,
                request.UserId);
            return CreateEvent(request, cached);
        }

        var enrichmentLock = await resultStore.TryAcquireLockAsync(request.TrackId, ct)
            ?? throw new EnrichmentLockUnavailableException(request.TrackId);

        await using (enrichmentLock)
        {
            cached = await resultStore.GetAsync(request.TrackId, ct);
            if (cached is not null)
            {
                return CreateEvent(request, cached);
            }

            var connection = await connectionClient.GetRequiredAsync(request.UserId, ct);
            var lyrics = await lyricsClient.GetPlainLyricsAsync(
                request.Title,
                request.Artist,
                request.Album,
                request.DurationSeconds,
                ct);

            var analysis = await analysisService.AnalyzeAsync(
                new TrackAnalysisRequest(
                    request.Title,
                    request.Artist,
                    request.Album,
                    request.DurationSeconds,
                    lyrics),
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
                lyrics,
                analysis.PrimaryMood,
                analysis.Themes,
                analysis.EnergyLevel,
                summary);

            await resultStore.SetAsync(request.TrackId, result, ct);
            _logger?.LogInformation(
                "Enriched track {TrackId} for user {UserId}.",
                request.TrackId,
                request.UserId);

            return CreateEvent(request, result);
        }
    }

    private SongEnrichedEvent CreateEvent(
        SongImportedEvent request,
        TrackEnrichmentResult result) =>
        new(
            request.TrackId,
            request.UserId,
            result.Lyrics,
            result.PrimaryMood,
            result.Themes,
            result.EnergyLevel,
            result.SemanticSummary,
            _timeProvider.GetUtcNow());
}
