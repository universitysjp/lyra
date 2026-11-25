namespace Lyra.PlaylistRag.Api.Tests.TestDoubles;

using Lyra.AI;
using Lyra.Contracts.AI;
using Lyra.PlaylistRag.Api.Models.Playlists;
using Lyra.PlaylistRag.Api.Services.Playlists;

internal sealed class FakePlaylistQueryPlanner(Func<PlaylistQueryPlan>? factory = null)
    : IPlaylistQueryPlanner
{
    public int Calls { get; private set; }

    public string? LastPrompt { get; private set; }

    public AiConnectionSecret? LastConnection { get; private set; }

    public string? LastSessionId { get; private set; }

    public Task<PlaylistQueryPlan> PlanAsync(
        string prompt,
        AiConnectionSecret connection,
        string sessionId,
        CancellationToken ct)
    {
        Calls++;
        LastPrompt = prompt;
        LastConnection = connection;
        LastSessionId = sessionId;

        return Task.FromResult(factory?.Invoke() ?? Plan());
    }

    public static PlaylistQueryPlan Plan(
        string semanticQuery = "warm evening songs",
        string? artist = null,
        string? title = null) =>
        new(
            semanticQuery,
            ["calm"],
            ["evening"],
            2,
            7,
            artist,
            title,
            "Evening glow",
            "A calm set for the end of the day.");
}
