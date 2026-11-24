namespace Lyra.Enrichment.Worker.Services.Lyrics;

using Lyra.Contracts.Music;

/// <summary>Lyrics returned by LRCLIB plus the metadata of the selected record.</summary>
public sealed record LyricsResult(
    string? PlainLyrics,
    string? SyncedLyrics,
    ResolvedTrackMetadata? Metadata = null)
{
    public static LyricsResult Empty { get; } = new(null, null);

    public bool HasLyrics =>
        !string.IsNullOrWhiteSpace(PlainLyrics) || !string.IsNullOrWhiteSpace(SyncedLyrics);
}
