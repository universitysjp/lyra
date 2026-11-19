namespace Lyra.Contracts.Events;

/// <summary>Published by the enrichment worker once a track has been processed.</summary>
public sealed record SongEnrichedEvent(
    Guid TrackId,
    Guid UserId,
    string? Lyrics,
    string PrimaryMood,
    IReadOnlyList<string> Themes,
    int EnergyLevel,
    string SemanticSummary,
    DateTimeOffset EnrichedAt);
