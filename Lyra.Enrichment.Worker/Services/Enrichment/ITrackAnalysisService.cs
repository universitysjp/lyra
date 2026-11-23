namespace Lyra.Enrichment.Worker.Services.Enrichment;

using Lyra.Contracts.AI;
using Lyra.Enrichment.Worker.Models;

public interface ITrackAnalysisService
{
    Task<TrackAnalysis> AnalyzeAsync(
        TrackAnalysisRequest request,
        AiConnectionSecret connection,
        string sessionId,
        CancellationToken ct);
}
