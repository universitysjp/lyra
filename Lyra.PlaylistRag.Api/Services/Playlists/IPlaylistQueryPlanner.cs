namespace Lyra.PlaylistRag.Api.Services.Playlists;

using Lyra.Contracts.AI;
using Lyra.PlaylistRag.Api.Models.Playlists;

public interface IPlaylistQueryPlanner
{
    Task<PlaylistQueryPlan> PlanAsync(
        string prompt,
        AiConnectionSecret connection,
        string sessionId,
        CancellationToken ct);
}
