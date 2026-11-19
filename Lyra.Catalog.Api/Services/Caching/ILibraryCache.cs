namespace Lyra.Catalog.Api.Services.Caching;

using Lyra.Catalog.Api.DTOs.Responses;

public interface ILibraryCache
{
    Task<PagedResponse<TrackResponse>?> GetLibraryAsync(
        Guid userId, int page, int pageSize, CancellationToken ct);

    Task SetLibraryAsync(
        Guid userId, PagedResponse<TrackResponse> response, CancellationToken ct);

    /// <summary>Invalidates every cached page for the user by bumping their version counter.</summary>
    Task InvalidateAsync(Guid userId, CancellationToken ct);
}
