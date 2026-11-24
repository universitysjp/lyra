namespace Lyra.Catalog.Api.Services.Caching;

using System.Text.Json;
using Lyra.Catalog.Api.Configuration;
using Lyra.Catalog.Api.DTOs.Responses;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

public sealed class LibraryCache(
    IDistributedCache cache,
    IOptions<CatalogOptions>? catalogOptions = null) : ILibraryCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly CatalogOptions _catalog = catalogOptions?.Value ?? new CatalogOptions();

    public async Task<LibraryCacheLookup> GetLibraryAsync(
        Guid userId, int page, int pageSize, CancellationToken ct)
    {
        var version = await GetVersionAsync(userId, ct);
        var key = BuildLibraryKey(userId, version, page, pageSize);

        var json = await cache.GetStringAsync(key, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new LibraryCacheLookup(version, null);
        }

        var response = JsonSerializer.Deserialize<PagedResponse<TrackResponse>>(
            json,
            SerializerOptions);

        return new LibraryCacheLookup(version, response);
    }

    public async Task SetLibraryAsync(
        Guid userId,
        string version,
        PagedResponse<TrackResponse> response,
        CancellationToken ct)
    {
        var key = BuildLibraryKey(userId, version, response.Page, response.PageSize);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_catalog.LibraryCacheMinutes)
        };

        await cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(response, SerializerOptions),
            options,
            ct);
    }

    public Task InvalidateAsync(Guid userId, CancellationToken ct) =>
        cache.SetStringAsync(
            BuildVersionKey(userId),
            Guid.NewGuid().ToString("N"),
            new DistributedCacheEntryOptions(),
            ct);

    private async Task<string> GetVersionAsync(Guid userId, CancellationToken ct) =>
        await cache.GetStringAsync(BuildVersionKey(userId), ct) ?? "0";

    private static string BuildVersionKey(Guid userId) => $"catalog:tracks:{userId}:ver";

    private static string BuildLibraryKey(Guid userId, string version, int page, int pageSize) =>
        $"catalog:tracks:{userId}:v{version}:p{page}:s{pageSize}";
}
