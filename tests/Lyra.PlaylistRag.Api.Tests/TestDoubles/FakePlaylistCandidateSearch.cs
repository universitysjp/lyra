namespace Lyra.PlaylistRag.Api.Tests.TestDoubles;

using Lyra.PlaylistRag.Api.Models.Playlists;
using Lyra.PlaylistRag.Api.Services.Playlists;

internal sealed class FakePlaylistCandidateSearch(
    Func<Guid, PlaylistQueryPlan, int, PlaylistCandidateSearchResult>? handler = null)
    : IPlaylistCandidateSearch
{
    public int Calls { get; private set; }

    public Guid? LastUserId { get; private set; }

    public PlaylistQueryPlan? LastPlan { get; private set; }

    public int? LastLimit { get; private set; }

    public ReadOnlyMemory<float> LastEmbedding { get; private set; }

    public Task<PlaylistCandidateSearchResult> SearchAsync(
        Guid userId,
        PlaylistQueryPlan plan,
        ReadOnlyMemory<float> queryEmbedding,
        int limit,
        CancellationToken ct)
    {
        Calls++;
        LastUserId = userId;
        LastPlan = plan;
        LastLimit = limit;
        LastEmbedding = queryEmbedding;

        return Task.FromResult(
            handler?.Invoke(userId, plan, limit)
            ?? new PlaylistCandidateSearchResult(
                [Candidate("local-1", "First", "Artist", 1), Candidate("local-2", "Second", "Other", 2)],
                HasIndexedTracks: true));
    }

    public static PlaylistCandidate Candidate(
        string localTrackId,
        string title,
        string artist,
        int index) =>
        new(
            index,
            Guid.NewGuid(),
            localTrackId,
            title,
            artist,
            "Album",
            180,
            "calm",
            ["evening"],
            3,
            "A calm evening track.",
            0.1 * index);
}
