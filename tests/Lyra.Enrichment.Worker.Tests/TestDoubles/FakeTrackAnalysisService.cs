namespace Lyra.Enrichment.Worker.Tests.TestDoubles;

using Lyra.Contracts.AI;
using Lyra.Enrichment.Worker.Models;
using Lyra.Enrichment.Worker.Services.Enrichment;

internal sealed class FakeTrackAnalysisService(
    Func<TrackAnalysisRequest, AiConnectionSecret, string, CancellationToken, Task<TrackAnalysis>> handler)
    : ITrackAnalysisService
{
    public int Calls { get; private set; }

    public TrackAnalysisRequest? LastRequest { get; private set; }

    public AiConnectionSecret? LastConnection { get; private set; }

    public string? LastSessionId { get; private set; }

    public Task<TrackAnalysis> AnalyzeAsync(
        TrackAnalysisRequest request,
        AiConnectionSecret connection,
        string sessionId,
        CancellationToken ct)
    {
        Calls++;
        LastRequest = request;
        LastConnection = connection;
        LastSessionId = sessionId;
        return handler(request, connection, sessionId, ct);
    }
}
