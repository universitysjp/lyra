namespace Lyra.Enrichment.Worker.Tests.TestDoubles;

using Lyra.Contracts.Events;
using Lyra.Enrichment.Worker.Services.Enrichment;

internal sealed class FakeTrackEnrichmentService(
    Func<SongImportedEvent, string, CancellationToken, Task<SongEnrichedEvent>> handler)
    : ITrackEnrichmentService
{
    public string? LastSessionId { get; private set; }

    public int Calls { get; private set; }

    public Task<SongEnrichedEvent> EnrichAsync(
        SongImportedEvent request,
        string sessionId,
        CancellationToken ct)
    {
        Calls++;
        LastSessionId = sessionId;
        return handler(request, sessionId, ct);
    }
}
