namespace Lyra.Catalog.Api.Repositories;

using Lyra.Catalog.Api.DTOs.Responses;
using Lyra.Catalog.Api.Models;

public interface ITrackRepository
{
    /// <summary>
    /// Finds existing global tracks by fingerprint and stages any missing tracks for insertion.
    /// Returns a fingerprint-to-track-id map covering both existing and newly created tracks.
    /// </summary>
    Task<Dictionary<string, Guid>> UpsertTracksAsync(
        IReadOnlyCollection<Track> tracks, CancellationToken ct);

    /// <summary>Returns the fingerprints of the supplied tracks that are already linked to the user.</summary>
    Task<HashSet<string>> GetLinkedFingerprintsAsync(
        Guid userId, IReadOnlyCollection<string> fingerprints, CancellationToken ct);

    /// <summary>Stages user-track links. Already linked tracks are skipped.</summary>
    Task LinkUserTracksAsync(
        Guid userId,
        IReadOnlyCollection<(Guid TrackId, string LocalTrackId, DateTimeOffset ImportedAt)> links,
        CancellationToken ct);

    /// <summary>Returns a page of the user's library, newest first, plus the total count.</summary>
    Task<(IReadOnlyList<TrackResponse> Items, int TotalCount)> GetPagedAsync(
        Guid userId, int page, int pageSize, CancellationToken ct);

    /// <summary>Fuzzy title/artist search strictly scoped to the user through the user-track link.</summary>
    Task<IReadOnlyList<TrackResponse>> SearchFuzzyAsync(
        Guid userId, string query, double threshold, int limit, CancellationToken ct);

    /// <summary>Commits all staged changes as a single transaction.</summary>
    Task SaveChangesAsync(CancellationToken ct);
}
