namespace Lyra.Enrichment.Worker.Configuration;

public sealed class LrclibOptions
{
    public const string SectionName = "Lrclib";

    public string BaseUrl { get; set; } = "https://lrclib.net";

    public int RequestTimeoutSeconds { get; set; } = 15;

    public int MinRequestIntervalMilliseconds { get; set; } = 300;

    public string UserAgent { get; set; } = "Lyra.Enrichment.Worker/1.0";

    public int MaxFuzzyCandidates { get; set; } = 25;

    public int MaxCandidateDetailFetches { get; set; } = 3;

    public double FuzzyMatchThreshold { get; set; } = 85;

    public double DurationToleranceSeconds { get; set; } = 20;

    public int SuccessCacheMinutes { get; set; } = 1440;

    public int NotFoundCacheMinutes { get; set; } = 360;
}
