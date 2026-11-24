namespace Lyra.Catalog.Api.Repositories;

using Lyra.Catalog.Api.Data;
using Lyra.Catalog.Api.DTOs.Responses;
using Lyra.Catalog.Api.Models;
using Lyra.Contracts.Music;
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
                ut.Track.Lyrics != null || ut.Track.SyncedLyrics != null,
                ut.Track.SyncedLyrics != null,
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
                    (t."Lyrics" IS NOT NULL OR t."SyncedLyrics" IS NOT NULL) AS "HasLyrics",
                    (t."SyncedLyrics" IS NOT NULL) AS "HasSyncedLyrics",
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
                row.HasLyrics,
                row.HasSyncedLyrics,
                row.ImportedAt))
            .ToList();
    }

    public async Task<TrackLyricsResponse?> GetLyricsAsync(
        Guid userId, Guid trackId, CancellationToken ct)
    {
        return await db.UserTracks
            .AsNoTracking()
            .Where(link => link.UserId == userId && link.TrackId == trackId)
            .Select(link => new TrackLyricsResponse(
                link.TrackId,
                link.Track.IsEnriched,
                link.Track.Lyrics,
                link.Track.SyncedLyrics))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<TrackLyricsApplyResult?> ApplyLyricsAsync(
        Guid trackId,
        string? lyrics,
        string? syncedLyrics,
        ResolvedTrackMetadata? resolvedMetadata,
        CancellationToken ct)
    {
        var current = await db.Tracks
            .AsNoTracking()
            .Where(track => track.Id == trackId)
            .Select(track => new
            {
                track.Lyrics,
                track.SyncedLyrics,
                track.ResolvedTitle,
                track.ResolvedArtist,
                track.ResolvedAlbum,
                track.ResolvedDurationSeconds,
                track.ResolvedInstrumental
            })
            .SingleOrDefaultAsync(ct);

        if (current is null)
        {
            return null;
        }

        var nextLyrics = string.IsNullOrWhiteSpace(lyrics) ? current.Lyrics : lyrics;
        var nextSynced = string.IsNullOrWhiteSpace(syncedLyrics)
            ? current.SyncedLyrics
            : syncedLyrics;
        var nextTitle = string.IsNullOrWhiteSpace(resolvedMetadata?.Title)
            ? current.ResolvedTitle
            : resolvedMetadata!.Title;
        var nextArtist = string.IsNullOrWhiteSpace(resolvedMetadata?.Artist)
            ? current.ResolvedArtist
            : resolvedMetadata!.Artist;
        var nextAlbum = string.IsNullOrWhiteSpace(resolvedMetadata?.Album)
            ? current.ResolvedAlbum
            : resolvedMetadata!.Album;
        var nextDuration = resolvedMetadata?.DurationSeconds is > 0
            ? resolvedMetadata.DurationSeconds
            : current.ResolvedDurationSeconds;
        var nextInstrumental = resolvedMetadata is null
            ? current.ResolvedInstrumental
            : resolvedMetadata.Instrumental;

        var changed = nextLyrics != current.Lyrics
            || nextSynced != current.SyncedLyrics
            || nextTitle != current.ResolvedTitle
            || nextArtist != current.ResolvedArtist
            || nextAlbum != current.ResolvedAlbum
            || nextDuration != current.ResolvedDurationSeconds
            || nextInstrumental != current.ResolvedInstrumental;

        if (changed)
        {
            await db.Tracks
                .Where(track => track.Id == trackId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(track => track.Lyrics, nextLyrics)
                    .SetProperty(track => track.SyncedLyrics, nextSynced)
                    .SetProperty(track => track.ResolvedTitle, nextTitle)
                    .SetProperty(track => track.ResolvedArtist, nextArtist)
                    .SetProperty(track => track.ResolvedAlbum, nextAlbum)
                    .SetProperty(track => track.ResolvedDurationSeconds, nextDuration)
                    .SetProperty(track => track.ResolvedInstrumental, nextInstrumental),
                    ct);
        }

        var userIds = await db.UserTracks
            .AsNoTracking()
            .Where(link => link.TrackId == trackId)
            .Select(link => link.UserId)
            .Distinct()
            .ToListAsync(ct);

        return new TrackLyricsApplyResult(changed, userIds);
    }

    public async Task<TrackEnrichmentApplyResult?> ApplyEnrichmentAsync(
        Guid trackId,
        TrackEnrichmentContent content,
        CancellationToken ct)
    {
        var resolvedTitle = content.ResolvedMetadata?.Title;
        var resolvedArtist = content.ResolvedMetadata?.Artist;
        var resolvedAlbum = content.ResolvedMetadata?.Album;
        var resolvedDuration = content.ResolvedMetadata?.DurationSeconds;
        var resolvedInstrumental = content.ResolvedMetadata?.Instrumental;

        var updated = await db.Tracks
            .Where(track => track.Id == trackId && !track.IsEnriched)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(track => track.Lyrics, track => content.Lyrics ?? track.Lyrics)
                .SetProperty(track => track.SyncedLyrics, track => content.SyncedLyrics ?? track.SyncedLyrics)
                .SetProperty(track => track.ResolvedTitle, track => resolvedTitle ?? track.ResolvedTitle)
                .SetProperty(track => track.ResolvedArtist, track => resolvedArtist ?? track.ResolvedArtist)
                .SetProperty(track => track.ResolvedAlbum, track => resolvedAlbum ?? track.ResolvedAlbum)
                .SetProperty(track => track.ResolvedDurationSeconds, track => resolvedDuration ?? track.ResolvedDurationSeconds)
                .SetProperty(track => track.ResolvedInstrumental, track => resolvedInstrumental ?? track.ResolvedInstrumental)
                .SetProperty(track => track.PrimaryMood, content.PrimaryMood)
                .SetProperty(track => track.Themes, content.ThemesJson)
                .SetProperty(track => track.EnergyLevel, content.EnergyLevel)
                .SetProperty(track => track.SemanticSummary, content.SemanticSummary)
                .SetProperty(track => track.IsEnriched, true),
                ct);

        var exists = updated > 0
            || await db.Tracks.AsNoTracking().AnyAsync(track => track.Id == trackId, ct);

        if (!exists)
        {
            return null;
        }

        var userIds = await db.UserTracks
            .AsNoTracking()
            .Where(link => link.TrackId == trackId)
            .Select(link => link.UserId)
            .Distinct()
            .ToListAsync(ct);

        return new TrackEnrichmentApplyResult(updated > 0, userIds);
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

        public bool HasLyrics { get; set; }

        public bool HasSyncedLyrics { get; set; }

        public DateTimeOffset ImportedAt { get; set; }
    }
}
