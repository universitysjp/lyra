namespace Lyra.Catalog.Api.Repositories;

public sealed record TrackEnrichmentApplyResult(
    bool Applied,
    IReadOnlyList<Guid> LinkedUserIds);
