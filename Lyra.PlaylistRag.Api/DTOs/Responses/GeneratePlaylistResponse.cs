namespace Lyra.PlaylistRag.Api.DTOs.Responses;

public sealed record GeneratePlaylistResponse(
    string Title,
    string Description,
    IReadOnlyList<PlaylistTrackResponse> Tracks);
