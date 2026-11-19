namespace Lyra.Contracts.Events;

/// <summary>Published by the catalog service when a user imports a new track.</summary>
public sealed record SongImportedEvent(
    Guid TrackId,
    Guid UserId,
    string LocalTrackId,
    string Title,
    string Artist,
    string? Album,
    int DurationSeconds,
    DateTimeOffset ImportedAt);
