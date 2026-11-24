namespace Lyra.Catalog.Api.Services.Caching;

using Lyra.Catalog.Api.DTOs.Responses;

/// <summary>
/// A cache lookup and the exact version token used for it. Callers must pass the
/// same token when filling a miss so a concurrent invalidation cannot make a stale
/// database result current.
/// </summary>
public sealed record LibraryCacheLookup(
    string Version,
    PagedResponse<TrackResponse>? Response);
