namespace Lyra.Catalog.Api.Services.Catalog;

using Lyra.Catalog.Api.DTOs.Responses;

public interface ITrackQueryService
{
    Task<PagedResponse<TrackResponse>> GetLibraryAsync(
        Guid userId, int page, int pageSize, CancellationToken ct);

    Task<IReadOnlyList<TrackResponse>> SearchAsync(
        Guid userId, string query, CancellationToken ct);

    Task<TrackLyricsResponse> GetLyricsAsync(
        Guid userId, Guid trackId, CancellationToken ct);
}
