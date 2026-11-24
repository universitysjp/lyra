namespace Lyra.Enrichment.Worker.Services.Enrichment;

public sealed class EnrichmentLockUnavailableException(Guid trackId)
    : Exception($"Another worker is currently enriching track {trackId}.")
{
    public Guid TrackId { get; } = trackId;
}
