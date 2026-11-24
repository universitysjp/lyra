namespace Lyra.Enrichment.Worker.Services.Lyrics;

public sealed record LrclibLyricsResponse(
    long Id,
    string? TrackName,
    string? ArtistName,
    string? AlbumName,
    double? Duration,
    bool Instrumental,
    string? PlainLyrics,
    string? SyncedLyrics);
