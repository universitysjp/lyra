namespace Lyra.PlaylistRag.Api.Services.Playlists;

using Lyra.PlaylistRag.Api.Configuration;
using Lyra.PlaylistRag.Api.Data;
using Lyra.PlaylistRag.Api.Models.Playlists;
using Lyra.PlaylistRag.Api.Services.Embeddings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;

/// <summary>
/// User-scoped pgvector search. Ownership is joined through user_tracks, so a track
/// owned by another user can never become a candidate. Results are diversified before
/// the model sees them, limiting repeated artists while preserving distance order.
/// </summary>
public sealed class PgvectorPlaylistCandidateSearch(
    VectorDbContext db,
    ITrackEmbeddingGenerator embeddingGenerator,
    IOptions<PlaylistOptions>? options = null) : IPlaylistCandidateSearch
{
    private readonly PlaylistOptions _options = options?.Value ?? new PlaylistOptions();

    public async Task<PlaylistCandidateSearchResult> SearchAsync(
        Guid userId,
        PlaylistQueryPlan plan,
        ReadOnlyMemory<float> queryEmbedding,
        int limit,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user id is required.", nameof(userId));
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        if (queryEmbedding.Length != embeddingGenerator.Dimensions)
        {
            throw new InvalidOperationException(
                $"The query embedding has {queryEmbedding.Length} dimensions, but the embedding model uses {embeddingGenerator.Dimensions}.");
        }

        var queryVector = new Vector(queryEmbedding.ToArray());
        var owned = from userTrack in db.UserTracks.AsNoTracking()
                    join trackEmbedding in db.TrackEmbeddings.AsNoTracking()
                        on userTrack.TrackId equals trackEmbedding.TrackId
                    where userTrack.UserId == userId
                        && trackEmbedding.EmbeddingModel == embeddingGenerator.Model
                        && trackEmbedding.EmbeddingDimensions == embeddingGenerator.Dimensions
                    select new { userTrack, trackEmbedding };

        var hasIndexedTracks = await owned.AnyAsync(ct);
        if (!hasIndexedTracks)
        {
            return new PlaylistCandidateSearchResult([], HasIndexedTracks: false);
        }

        var filtered = owned;

        // Moods and themes are deliberately soft ranking signals. Stored enrichment values
        // are free-text phrases (for example "Flirtatious, upbeat romantic dance-pop"), so
        // treating planner output as exact SQL filters discards valid matches. Energy and
        // explicit artist/title remain hard constraints.
        if (plan.MinEnergy is not null)
        {
            filtered = filtered.Where(item =>
                item.trackEmbedding.EnergyLevel != null
                && item.trackEmbedding.EnergyLevel >= plan.MinEnergy);
        }

        if (plan.MaxEnergy is not null)
        {
            filtered = filtered.Where(item =>
                item.trackEmbedding.EnergyLevel != null
                && item.trackEmbedding.EnergyLevel <= plan.MaxEnergy);
        }

        if (!string.IsNullOrWhiteSpace(plan.Artist))
        {
            var artist = plan.Artist.ToLowerInvariant();
            filtered = filtered.Where(item =>
                item.trackEmbedding.Artist.ToLower().Contains(artist));
        }

        if (!string.IsNullOrWhiteSpace(plan.Title))
        {
            var title = plan.Title.ToLowerInvariant();
            filtered = filtered.Where(item =>
                item.trackEmbedding.Title.ToLower().Contains(title));
        }

        var poolSize = Math.Clamp(
            checked(limit * Math.Max(1, _options.CandidatePoolMultiplier)),
            limit,
            Math.Max(limit, _options.MaxCandidatePool));

        var rows = await filtered
            .Select(item => new
            {
                item.userTrack.TrackId,
                item.userTrack.LocalTrackId,
                item.trackEmbedding.Title,
                item.trackEmbedding.Artist,
                item.trackEmbedding.Album,
                item.trackEmbedding.DurationSeconds,
                item.trackEmbedding.PrimaryMood,
                item.trackEmbedding.Themes,
                item.trackEmbedding.EnergyLevel,
                item.trackEmbedding.SemanticSummary,
                Distance = item.trackEmbedding.Embedding.CosineDistance(queryVector)
            })
            .OrderBy(item => item.Distance)
            .Take(poolSize)
            .ToListAsync(ct);

        var descriptorTerms = plan.Moods
            .Concat(plan.Themes)
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Select(term => term.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (descriptorTerms.Length > 0)
        {
            rows = rows
                .OrderByDescending(row => DescriptorMatchCount(
                    row.PrimaryMood,
                    row.Themes,
                    row.SemanticSummary,
                    descriptorTerms))
                .ThenBy(row => row.Distance)
                .ToList();
        }

        var candidates = new List<PlaylistCandidate>();
        var perArtist = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            perArtist.TryGetValue(row.Artist, out var artistCount);
            if (artistCount >= _options.MaxCandidatesPerArtist)
            {
                continue;
            }

            perArtist[row.Artist] = artistCount + 1;
            candidates.Add(new PlaylistCandidate(
                candidates.Count + 1,
                row.TrackId,
                row.LocalTrackId,
                row.Title,
                row.Artist,
                row.Album,
                row.DurationSeconds,
                row.PrimaryMood,
                row.Themes,
                row.EnergyLevel,
                row.SemanticSummary,
                row.Distance));

            if (candidates.Count >= _options.MaxCandidatePool)
            {
                break;
            }
        }

        return new PlaylistCandidateSearchResult(candidates, HasIndexedTracks: true);
    }

    private static int DescriptorMatchCount(
        string? primaryMood,
        IReadOnlyList<string> themes,
        string semanticSummary,
        IReadOnlyList<string> terms)
    {
        var count = 0;
        foreach (var term in terms)
        {
            if (Contains(primaryMood, term)
                || Contains(semanticSummary, term)
                || themes.Any(theme => Contains(theme, term) || Contains(term, theme)))
            {
                count++;
            }
        }

        return count;
    }

    private static bool Contains(string? value, string term) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains(term, StringComparison.OrdinalIgnoreCase);
}
