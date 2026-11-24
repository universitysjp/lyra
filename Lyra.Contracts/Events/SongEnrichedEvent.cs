namespace Lyra.Contracts.Events;

using Lyra.Contracts.Music;

/// <summary>Published by the enrichment worker once a track has been processed.</summary>
public sealed record SongEnrichedEvent(
    Guid TrackId,
    Guid UserId,
    string? Lyrics,
    string? SyncedLyrics,
    string PrimaryMood,
    IReadOnlyList<string> Themes,
    int EnergyLevel,
    string SemanticSummary,
    DateTimeOffset EnrichedAt,
    ResolvedTrackMetadata? ResolvedMetadata = null);
