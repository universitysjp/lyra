namespace Lyra.Enrichment.Worker.Models;

/// <summary>
/// User-independent enrichment content cached by global track ID. It contains
/// no credentials and no user identifiers.
/// </summary>
public sealed record TrackEnrichmentResult(
    string? Lyrics,
    string PrimaryMood,
    IReadOnlyList<string> Themes,
    int EnergyLevel,
    string SemanticSummary);
