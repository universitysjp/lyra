namespace Lyra.Enrichment.Worker.Services.Caching;

using Lyra.Enrichment.Worker.Models;

public interface IEnrichmentResultStore
{
    Task<TrackEnrichmentResult?> GetAsync(Guid trackId, CancellationToken ct);

    Task SetAsync(
        Guid trackId,
        TrackEnrichmentResult result,
        CancellationToken ct);

    Task<ITrackEnrichmentLock?> TryAcquireLockAsync(
        Guid trackId,
        CancellationToken ct);
}
