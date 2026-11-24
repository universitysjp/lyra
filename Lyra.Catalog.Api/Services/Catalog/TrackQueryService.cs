namespace Lyra.Catalog.Api.Services.Catalog;

using Lyra.Catalog.Api.Configuration;
using Lyra.Catalog.Api.DTOs.Responses;
using Lyra.Catalog.Api.Exceptions;
using Lyra.Catalog.Api.Repositories;
using Lyra.Catalog.Api.Services.Caching;
using Microsoft.Extensions.Options;

public sealed class TrackQueryService(
    ITrackRepository repository,
    ILibraryCache libraryCache,
    IOptions<CatalogOptions>? catalogOptions = null,
    IOptions<AppStringOptions>? stringOptions = null) : ITrackQueryService
{
    private readonly CatalogOptions _catalog = catalogOptions?.Value ?? new CatalogOptions();
    private readonly AppStringOptions _strings = stringOptions?.Value ?? new AppStringOptions();

    public async Task<PagedResponse<TrackResponse>> GetLibraryAsync(
        Guid userId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = pageSize <= 0
            ? _catalog.DefaultPageSize
            : Math.Min(pageSize, _catalog.MaxPageSize);

        var cached = await libraryCache.GetLibraryAsync(userId, page, pageSize, ct);
        if (cached.Response is not null)
        {
            return cached.Response;
        }

        var (items, totalCount) = await repository.GetPagedAsync(userId, page, pageSize, ct);
        var response = new PagedResponse<TrackResponse>(items, page, pageSize, totalCount);

        // Reuse the version from the miss. If an import invalidated the cache while
        // the database query was running, this stale response remains under the old
        // version and can never be served as current.
        await libraryCache.SetLibraryAsync(userId, cached.Version, response, ct);

        return response;
    }

    public Task<IReadOnlyList<TrackResponse>> SearchAsync(
        Guid userId, string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ValidationException(_strings.Errors.InvalidSearchQuery);
        }

        return repository.SearchFuzzyAsync(
            userId,
            query.Trim(),
            _catalog.SearchSimilarityThreshold,
            _catalog.MaxPageSize,
            ct);
    }

    public async Task<TrackLyricsResponse> GetLyricsAsync(
        Guid userId, Guid trackId, CancellationToken ct) =>
        await repository.GetLyricsAsync(userId, trackId, ct)
        ?? throw new NotFoundException(_strings.Errors.TrackNotFound);
}
