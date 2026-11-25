namespace Lyra.PlaylistRag.Api.Tests.TestDoubles;

using Lyra.Contracts.AI;
using Lyra.PlaylistRag.Api.Models.Playlists;
using Lyra.PlaylistRag.Api.Services.Playlists;

internal sealed class FakePlaylistSelector(
    Func<int, PlaylistSelection>? factory = null) : IPlaylistSelector
{
    public int Calls { get; private set; }

    public string? LastPrompt { get; private set; }

    public IReadOnlyList<PlaylistCandidate>? LastCandidates { get; private set; }

    public int? LastLimit { get; private set; }

    public Task<PlaylistSelection> SelectAsync(
        string prompt,
        PlaylistQueryPlan plan,
        IReadOnlyList<PlaylistCandidate> candidates,
        int limit,
        AiConnectionSecret connection,
        string sessionId,
        CancellationToken ct)
    {
        Calls++;
        LastPrompt = prompt;
        LastCandidates = candidates;
        LastLimit = limit;

        var selection = factory?.Invoke(candidates.Count)
            ?? new PlaylistSelection(
                candidates
                    .Take(Math.Min(limit, candidates.Count))
                    .Select((candidate, index) => new PlaylistSelectionItem(
                        candidate.Index,
                        $"Fits the request {index + 1}."))
                    .ToArray());

        return Task.FromResult(selection);
    }
}
