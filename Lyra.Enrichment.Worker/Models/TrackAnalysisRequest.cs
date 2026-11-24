namespace Lyra.Enrichment.Worker.Models;

public sealed record TrackAnalysisRequest(
    string Title,
    string Artist,
    string? Album,
    int DurationSeconds,
    string? Lyrics);
