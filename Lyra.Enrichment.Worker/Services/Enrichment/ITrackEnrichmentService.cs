namespace Lyra.Enrichment.Worker.Services.Enrichment;

using Lyra.Contracts.Events;

public interface ITrackEnrichmentService
{
    Task<SongEnrichedEvent> EnrichAsync(
        SongImportedEvent request,
        string sessionId,
        CancellationToken ct);
}
