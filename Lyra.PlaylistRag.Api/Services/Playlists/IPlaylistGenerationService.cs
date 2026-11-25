namespace Lyra.PlaylistRag.Api.Services.Playlists;

using Lyra.PlaylistRag.Api.DTOs.Requests;
using Lyra.PlaylistRag.Api.DTOs.Responses;

public interface IPlaylistGenerationService
{
    Task<GeneratePlaylistResponse> GenerateAsync(
        Guid userId,
        GeneratePlaylistRequest request,
        CancellationToken ct);
}
