namespace Lyra.Catalog.Api.Repositories;

public sealed record TrackLyricsApplyResult(
    bool Applied,
    IReadOnlyList<Guid> LinkedUserIds);
