namespace Lyra.PlaylistRag.Api.Services.Embeddings;

using Lyra.Contracts.Events;
using Lyra.PlaylistRag.Api.Models;
using Lyra.PlaylistRag.Api.Repositories;
using Microsoft.Extensions.Logging;
using Pgvector;

public sealed class TrackEmbeddingService(
    ITrackEmbeddingRepository repository,
    IUserTrackRepository userTracks,
    ITrackEmbeddingGenerator generator,
    TimeProvider? timeProvider = null,
    ILogger<TrackEmbeddingService>? logger = null) : ITrackEmbeddingService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task UpsertAsync(SongEnrichedEvent enriched, CancellationToken ct)
    {
        if (generator.Dimensions != TrackEmbedding.Dimensions)
        {
            throw new InvalidOperationException(
                $"The embedding generator returns {generator.Dimensions} dimensions, but vector_db expects {TrackEmbedding.Dimensions}.");
        }

        var local = enriched.ResolvedMetadata is null
            ? await userTracks.GetAsync(enriched.UserId, enriched.TrackId, ct)
            : null;

        var title = FirstNonEmpty(
            enriched.ResolvedMetadata?.Title,
            local?.Title,
            enriched.TrackId.ToString());
        var artist = FirstNonEmpty(
            enriched.ResolvedMetadata?.Artist,
            local?.Artist,
            "Unknown Artist");
        var album = enriched.ResolvedMetadata?.Album ?? local?.Album;
        var durationSeconds = enriched.ResolvedMetadata?.DurationSeconds is > 0
            ? (int)Math.Round(enriched.ResolvedMetadata.DurationSeconds.Value)
            : local?.DurationSeconds ?? 0;

        var text = BuildEmbeddingText(
            title,
            artist,
            album,
            enriched.PrimaryMood,
            enriched.Themes,
            enriched.EnergyLevel,
            enriched.SemanticSummary);

        var embedding = await generator.GenerateAsync(
            text,
            enriched.TrackId.ToString("N"),
            ct);
        if (embedding.Length != TrackEmbedding.Dimensions)
        {
            throw new InvalidOperationException(
                $"The embedding generator returned {embedding.Length} values, but vector_db expects {TrackEmbedding.Dimensions}.");
        }

        var now = _timeProvider.GetUtcNow();
        var entity = new TrackEmbedding
        {
            TrackId = enriched.TrackId,
            Title = title,
            Artist = artist,
            Album = album,
            DurationSeconds = durationSeconds,
            PrimaryMood = enriched.PrimaryMood,
            Themes = enriched.Themes.ToArray(),
            EnergyLevel = enriched.EnergyLevel,
            SemanticSummary = enriched.SemanticSummary,
            Embedding = new Vector(embedding.ToArray()),
            EmbeddingModel = generator.Model,
            EmbeddingDimensions = generator.Dimensions,
            EnrichedAt = enriched.EnrichedAt,
            UpdatedAt = now
        };

        await repository.UpsertAsync(entity, ct);

        logger?.LogInformation(
            "Stored embedding for track {TrackId} using model {Model} ({Dimensions} dimensions).",
            enriched.TrackId,
            generator.Model,
            generator.Dimensions);
    }

    private static string BuildEmbeddingText(
        string title,
        string artist,
        string? album,
        string mood,
        IReadOnlyList<string> themes,
        int energy,
        string summary) =>
        $"""
        Title: {title}
        Artist: {artist}
        Album: {album ?? "Unknown"}
        Mood: {mood}
        Themes: {string.Join(", ", themes)}
        Energy: {energy}
        Summary: {summary}
        """;

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim()
        ?? "Unknown";
}
