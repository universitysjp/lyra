namespace Lyra.Enrichment.Worker.Services.Lyrics;

/// <summary>
/// Candidate metadata from LRCLIB's v2 search endpoint. The beta v2 response does not
/// include lyric bodies; flags describe what a detail fetch can return.
/// </summary>
public sealed record LrclibSearchCandidate(
    long Id,
    string? TrackName,
    string? ArtistName,
    string? AlbumName,
    double? Duration,
    bool Instrumental,
    bool HasWordSync,
    bool HasPlainLyrics,
    bool HasSyncedLyrics,
    bool HasLyricsfile)
{
    public bool HasLyricsAvailability =>
        !Instrumental
        && (HasWordSync || HasPlainLyrics || HasSyncedLyrics || HasLyricsfile);
}
