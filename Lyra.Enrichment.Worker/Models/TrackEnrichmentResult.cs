namespace Lyra.Enrichment.Worker.Models;

using Lyra.Contracts.Music;

/// <summary>
/// User-independent enrichment content cached by global track ID. It contains
/// no credentials and no user identifiers.
/// </summary>
public sealed record TrackEnrichmentResult(
    string? Lyrics,
    string? SyncedLyrics,
    string PrimaryMood,
    IReadOnlyList<string> Themes,
    int EnergyLevel,
    string SemanticSummary,
    ResolvedTrackMetadata? ResolvedMetadata = null);
