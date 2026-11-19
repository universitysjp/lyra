namespace Lyra.Catalog.Api.Repositories;

using Lyra.Catalog.Api.Data;
using Lyra.Catalog.Api.DTOs.Responses;
using Lyra.Catalog.Api.Models;
using Microsoft.EntityFrameworkCore;

public sealed class TrackRepository(CatalogDbContext db) : ITrackRepository
{
    public async Task<Dictionary<string, Guid>> UpsertTracksAsync(
        IReadOnlyCollection<Track> tracks, CancellationToken ct)
    {
        var result = new Dictionary<string, Guid>(StringComparer.Ordinal);
        if (tracks.Count == 0)
        {
            return result;
        }

        var candidates = tracks
            .GroupBy(t => t.TrackFingerprint, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        var fingerprints = candidates.Select(t => t.TrackFingerprint).ToArray();

        var existing = await db.Tracks
            .AsNoTracking()
            .Where(t => fingerprints.Contains(t.TrackFingerprint))
            .Select(t => new { t.TrackFingerprint, t.Id })
            .ToListAsync(ct);

        foreach (var track in existing)
        {
            result[track.TrackFingerprint] = track.Id;
        }

        var missing = candidates.Where(t => !result.ContainsKey(t.TrackFingerprint)).ToList();
        foreach (var track in missing)
        {
            if (track.Id == Guid.Empty)
            {
                track.Id = Guid.NewGuid();
            }

            result[track.TrackFingerprint] = track.Id;
        }

        if (missing.Count > 0)
        {
            db.Tracks.AddRange(missing);
        }

        return result;
    }

    public async Task<HashSet<string>> GetLinkedFingerprintsAsync(
        Guid userId, IReadOnlyCollection<string> fingerprints, CancellationToken ct)
    {
        if (fingerprints.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var fingerprintArray = fingerprints.ToArray();

        var linked = await db.UserTracks
            .AsNoTracking()
            .Where(ut => ut.UserId == userId && fingerprintArray.Contains(ut.Track.TrackFingerprint))
            .Select(ut => ut.Track.TrackFingerprint)
            .ToListAsync(ct);

        return linked.ToHashSet(StringComparer.Ordinal);
    }

    public Task LinkUserTracksAsync(
        Guid userId,
        IReadOnlyCollection<(Guid TrackId, string LocalTrackId, DateTimeOffset ImportedAt)> links,
        CancellationToken ct)
    {
        if (links.Count == 0)
        {
            return Task.CompletedTask;
        }

        var entities = links
            .Select(link => new UserTrack
            {
                UserId = userId,
                TrackId = link.TrackId,
                LocalTrackId = link.LocalTrackId,
                ImportedAt = link.ImportedAt
            })
            .ToList();

        db.UserTracks.AddRange(entities);

        return Task.CompletedTask;
    }

    public async Task<(IReadOnlyList<TrackResponse> Items, int TotalCount)> GetPagedAsync(
        Guid userId, int page, int pageSize, CancellationToken ct)
    {
        var userTracks = db.UserTracks
            .AsNoTracking()
            .Where(ut => ut.UserId == userId);

        var totalCount = await userTracks.CountAsync(ct);
        if (totalCount == 0)
        {
            return (Array.Empty<TrackResponse>(), 0);
        }

        var items = await userTracks
            .OrderByDescending(ut => ut.ImportedAt)
            .ThenBy(ut => ut.TrackId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ut => new TrackResponse(
                ut.TrackId,
                ut.LocalTrackId,
                ut.Track.Title,
                ut.Track.Artist,
                ut.Track.Album,
                ut.Track.DurationSeconds,
                ut.Track.IsEnriched,
                ut.ImportedAt))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<TrackResponse>> SearchFuzzyAsync(
        Guid userId, string query, double threshold, int limit, CancellationToken ct)
    {
        if (limit <= 0)
        {
            return Array.Empty<TrackResponse>();
        }

        var rows = await db.Database
            .SqlQuery<TrackSearchRow>($"""
                SELECT
                    t."Id" AS "Id",
                    ut."LocalTrackId" AS "LocalTrackId",
                    t."Title" AS "Title",
                    t."Artist" AS "Artist",
                    t."Album" AS "Album",
                    t."DurationSeconds" AS "DurationSeconds",
                    t."IsEnriched" AS "IsEnriched",
                    ut."ImportedAt" AS "ImportedAt"
                FROM tracks AS t
                INNER JOIN user_tracks AS ut ON ut."TrackId" = t."Id"
                WHERE ut."UserId" = {userId}
                  AND (
                      similarity(t."Title", {query}) >= {threshold}
                      OR similarity(t."Artist", {query}) >= {threshold}
                  )
                ORDER BY GREATEST(
                    similarity(t."Title", {query}),
                    similarity(t."Artist", {query})
                ) DESC
                LIMIT {limit}
                """)
            .ToListAsync(ct);

        return rows
            .Select(row => new TrackResponse(
                row.Id,
                row.LocalTrackId,
                row.Title,
                row.Artist,
                row.Album,
                row.DurationSeconds,
                row.IsEnriched,
                row.ImportedAt))
            .ToList();
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private sealed class TrackSearchRow
    {
        public Guid Id { get; set; }

        public string LocalTrackId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Artist { get; set; } = string.Empty;

        public string? Album { get; set; }

        public int DurationSeconds { get; set; }

        public bool IsEnriched { get; set; }

        public DateTimeOffset ImportedAt { get; set; }
    }
}
