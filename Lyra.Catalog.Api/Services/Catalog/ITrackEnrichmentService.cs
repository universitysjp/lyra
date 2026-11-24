namespace Lyra.Catalog.Api.Services.Catalog;

using Lyra.Contracts.Events;

public interface ITrackEnrichmentService
{
    Task<bool> ApplyAsync(SongEnrichedEvent enriched, CancellationToken ct);
}
