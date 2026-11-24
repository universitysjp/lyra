namespace Lyra.Contracts.Music;

/// <summary>
/// Canonical metadata returned by LRCLIB for the candidate whose lyrics were selected.
/// Local ID3 metadata is often wrong, so this is the authoritative shape for downstream
/// playlist generation.
/// </summary>
public sealed record ResolvedTrackMetadata(
    string? Title,
    string? Artist,
    string? Album,
    double? DurationSeconds,
    bool Instrumental);
