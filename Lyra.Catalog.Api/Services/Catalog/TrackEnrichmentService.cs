namespace Lyra.Catalog.Api.Services.Catalog;

using System.Text.Json;
using Lyra.Catalog.Api.Configuration;
using Lyra.Catalog.Api.Exceptions;
using Lyra.Catalog.Api.Models;
using Lyra.Catalog.Api.Repositories;
using Lyra.Catalog.Api.Services.Caching;
using Lyra.Contracts.Events;
using Microsoft.Extensions.Options;

public sealed class TrackEnrichmentService(
    ITrackRepository repository,
    ILibraryCache libraryCache,
    IOptions<AppStringOptions>? stringOptions = null) : ITrackEnrichmentService
{
    private const int MaximumPrimaryMoodLength = 128;
    private readonly AppStringOptions _strings = stringOptions?.Value ?? new AppStringOptions();

    public async Task<bool> ApplyAsync(SongEnrichedEvent enriched, CancellationToken ct)
    {
        Validate(enriched);

        var content = new TrackEnrichmentContent(
            enriched.Lyrics,
            enriched.SyncedLyrics,
            enriched.PrimaryMood.Trim(),
            JsonSerializer.Serialize(enriched.Themes),
            enriched.EnergyLevel,
            enriched.SemanticSummary.Trim(),
            enriched.ResolvedMetadata);

        var result = await repository.ApplyEnrichmentAsync(enriched.TrackId, content, ct)
            ?? throw new NotFoundException(_strings.Errors.TrackNotFound);

        // Persist the EF consumer inbox even when this is a duplicate enrichment.
        await repository.SaveChangesAsync(ct);

        // Invalidating on duplicates as well makes cache maintenance idempotent when
        // the previous attempt committed the track but failed mid-invalidation.
        foreach (var userId in result.LinkedUserIds)
        {
            await libraryCache.InvalidateAsync(userId, ct);
        }

        return result.Applied;
    }

    private static void Validate(SongEnrichedEvent enriched)
    {
        if (enriched.TrackId == Guid.Empty
            || string.IsNullOrWhiteSpace(enriched.PrimaryMood)
            || enriched.PrimaryMood.Trim().Length > MaximumPrimaryMoodLength
            || enriched.Themes is null
            || enriched.Themes.Any(string.IsNullOrWhiteSpace)
            || enriched.EnergyLevel is < 1 or > 10
            || string.IsNullOrWhiteSpace(enriched.SemanticSummary))
        {
            throw new InvalidEnrichmentException(
                "The enriched event contains invalid track enrichment data.");
        }
    }
}
