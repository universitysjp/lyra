namespace Lyra.PlaylistRag.Api.Models.Playlists;

/// <summary>
/// Result of the user-scoped vector search. HasIndexedTracks distinguishes an empty
/// library from a valid query whose metadata filters matched nothing.
/// </summary>
public sealed record PlaylistCandidateSearchResult(
    IReadOnlyList<PlaylistCandidate> Candidates,
    bool HasIndexedTracks);
