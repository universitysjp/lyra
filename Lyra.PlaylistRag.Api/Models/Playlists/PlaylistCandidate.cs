namespace Lyra.PlaylistRag.Api.Models.Playlists;

/// <summary>One owned track that survived vector search and diversification.</summary>
public sealed record PlaylistCandidate(
    int Index,
    Guid TrackId,
    string LocalTrackId,
    string Title,
    string Artist,
    string? Album,
    int DurationSeconds,
    string? PrimaryMood,
    IReadOnlyList<string> Themes,
    int? EnergyLevel,
    string SemanticSummary,
    double Distance);
