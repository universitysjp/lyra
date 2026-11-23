namespace Lyra.Enrichment.Worker.Services.Lyrics;

public sealed record LrclibLyricsResponse(
    string? PlainLyrics,
    string? SyncedLyrics,
    bool Instrumental);
