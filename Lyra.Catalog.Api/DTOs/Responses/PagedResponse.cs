namespace Lyra.Catalog.Api.DTOs.Responses;

/// <summary>A single page of results with the total number of matching rows.</summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
