namespace Lyra.PlaylistRag.Api.Services.Playlists;

using Lyra.PlaylistRag.Api.Models.Playlists;

public interface IPlaylistCandidateSearch
{
    Task<PlaylistCandidateSearchResult> SearchAsync(
        Guid userId,
        PlaylistQueryPlan plan,
        ReadOnlyMemory<float> queryEmbedding,
        int limit,
        CancellationToken ct);
}
