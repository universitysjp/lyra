namespace Lyra.Contracts.Events;

using Lyra.Contracts.Music;

/// <summary>
/// Published by the enrichment worker as soon as lyrics are found, before any AI
/// provider call. Catalog stores them immediately so a later AI failure cannot
/// discard successfully retrieved lyrics.
/// </summary>
public sealed record SongLyricsFetchedEvent(
    Guid TrackId,
    Guid UserId,
    string? Lyrics,
    string? SyncedLyrics,
    DateTimeOffset FetchedAt,
    ResolvedTrackMetadata? ResolvedMetadata = null);
