namespace Lyra.PlaylistRag.Api.Services.Playlists;

using Lyra.Contracts.AI;
using Lyra.PlaylistRag.Api.Models.Playlists;

public interface IPlaylistSelector
{
    Task<PlaylistSelection> SelectAsync(
        string prompt,
        PlaylistQueryPlan plan,
        IReadOnlyList<PlaylistCandidate> candidates,
        int limit,
        AiConnectionSecret connection,
        string sessionId,
        CancellationToken ct);
}
