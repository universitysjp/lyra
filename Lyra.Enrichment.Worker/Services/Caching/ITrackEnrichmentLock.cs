namespace Lyra.Enrichment.Worker.Services.Caching;

public interface ITrackEnrichmentLock : IAsyncDisposable
{
    string OwnerToken { get; }
}
