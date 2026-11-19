namespace Lyra.Catalog.Api.DTOs.Responses;

/// <summary>A track in the caller's library, as seen through the user-track link.</summary>
public sealed record TrackResponse(
    Guid Id,
    string LocalTrackId,
    string Title,
    string Artist,
    string? Album,
    int DurationSeconds,
    bool IsEnriched,
    DateTimeOffset ImportedAt);
