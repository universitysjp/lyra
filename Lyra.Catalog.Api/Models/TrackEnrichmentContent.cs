namespace Lyra.Catalog.Api.Models;

using Lyra.Contracts.Music;

public sealed record TrackEnrichmentContent(
    string? Lyrics,
    string? SyncedLyrics,
    string PrimaryMood,
    string ThemesJson,
    int EnergyLevel,
    string SemanticSummary,
    ResolvedTrackMetadata? ResolvedMetadata = null);
