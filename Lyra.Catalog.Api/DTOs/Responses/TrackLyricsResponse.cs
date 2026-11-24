namespace Lyra.Catalog.Api.DTOs.Responses;

/// <summary>Lyrics for one owned track, fetched lazily by the player.</summary>
public sealed record TrackLyricsResponse(
    Guid TrackId,
    bool IsEnriched,
    string? PlainLyrics,
    string? SyncedLyrics);
