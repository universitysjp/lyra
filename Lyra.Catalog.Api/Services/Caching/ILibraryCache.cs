namespace Lyra.Catalog.Api.Services.Caching;

using Lyra.Catalog.Api.DTOs.Responses;

public interface ILibraryCache
{
    Task<LibraryCacheLookup> GetLibraryAsync(
        Guid userId, int page, int pageSize, CancellationToken ct);

    Task SetLibraryAsync(
        Guid userId,
        string version,
        PagedResponse<TrackResponse> response,
        CancellationToken ct);

    /// <summary>Invalidates every cached page for the user by replacing their version token.</summary>
    Task InvalidateAsync(Guid userId, CancellationToken ct);
}
