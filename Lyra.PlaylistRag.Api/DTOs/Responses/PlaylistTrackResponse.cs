namespace Lyra.PlaylistRag.Api.DTOs.Responses;

/// <summary>One generated playlist entry. LocalTrackId is the caller's own file identifier.</summary>
public sealed record PlaylistTrackResponse(
    string LocalTrackId,
    string Title,
    string Artist,
    string? Album,
    int DurationSeconds,
    string? Reason);
