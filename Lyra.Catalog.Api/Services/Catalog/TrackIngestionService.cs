namespace Lyra.Catalog.Api.Services.Catalog;

using Lyra.Catalog.Api.Configuration;
using Lyra.Catalog.Api.DTOs.Requests;
using Lyra.Catalog.Api.DTOs.Responses;
using Lyra.Catalog.Api.Exceptions;
using Lyra.Catalog.Api.Models;
using Lyra.Catalog.Api.Repositories;
using Lyra.Catalog.Api.Services.Caching;
using Lyra.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Options;

public sealed class TrackIngestionService(
    ITrackRepository repository,
    IPublishEndpoint publishEndpoint,
    ILibraryCache libraryCache,
    IOptions<CatalogOptions>? catalogOptions = null,
    IOptions<AppStringOptions>? stringOptions = null) : ITrackIngestionService
{
    private readonly CatalogOptions _catalog = catalogOptions?.Value ?? new CatalogOptions();
    private readonly AppStringOptions _strings = stringOptions?.Value ?? new AppStringOptions();

    public async Task<ImportTracksResponse> ImportBatchAsync(
        Guid userId, ImportTracksRequest request, CancellationToken ct)
    {
        var submitted = request.Tracks;
        if (submitted is null || submitted.Count == 0)
        {
            throw new ValidationException(_strings.Errors.EmptyBatch);
        }

        if (submitted.Count > _catalog.MaxBatchSize)
        {
            throw new ValidationException(_strings.Errors.BatchTooLarge);
        }

        var importedAt = DateTimeOffset.UtcNow;
        var pendingByFingerprint = new Dictionary<string, (Track Track, TrackMetadataItem Item)>(
            StringComparer.Ordinal);
        var submittedLocalIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in submitted)
        {
            if (string.IsNullOrWhiteSpace(item.LocalTrackId)
                || string.IsNullOrWhiteSpace(item.Title)
                || string.IsNullOrWhiteSpace(item.Artist))
            {
                throw new ValidationException(_strings.Errors.InvalidTrackMetadata);
            }

            var fingerprint = FingerprintGenerator.Compute(item.Title, item.Artist, item.DurationSeconds);
            if (pendingByFingerprint.ContainsKey(fingerprint) || !submittedLocalIds.Add(item.LocalTrackId))
            {
                continue;
            }

            pendingByFingerprint[fingerprint] = (new Track
            {
                Id = Guid.NewGuid(),
                Title = item.Title,
                Artist = item.Artist,
                Album = item.Album,
                DurationSeconds = item.DurationSeconds,
                TrackFingerprint = fingerprint,
                IsEnriched = false
            }, item);
        }

        var tracks = pendingByFingerprint.Values.Select(entry => entry.Track).ToList();
        var trackIds = await repository.UpsertTracksAsync(tracks, ct);
        var linkedFingerprints = await repository.GetLinkedFingerprintsAsync(
            userId, trackIds.Keys.ToArray(), ct);

        var links = new List<(Guid TrackId, string LocalTrackId, DateTimeOffset ImportedAt)>();
        var events = new List<SongImportedEvent>();

        foreach (var (track, item) in pendingByFingerprint.Values)
        {
            if (linkedFingerprints.Contains(track.TrackFingerprint))
            {
                continue;
            }

            var trackId = trackIds[track.TrackFingerprint];
            links.Add((trackId, item.LocalTrackId, importedAt));
            events.Add(new SongImportedEvent(
                trackId,
                userId,
                item.LocalTrackId,
                track.Title,
                track.Artist,
                track.Album,
                track.DurationSeconds,
                importedAt));
        }

        if (links.Count > 0)
        {
            await repository.LinkUserTracksAsync(userId, links, ct);

            foreach (var message in events)
            {
                await publishEndpoint.Publish(message, ct);
            }
        }

        await repository.SaveChangesAsync(ct);
        await libraryCache.InvalidateAsync(userId, ct);

        return new ImportTracksResponse(submitted.Count, links.Count, submitted.Count - links.Count);
    }
}
