namespace Lyra.Enrichment.Worker.Configuration;

public sealed class LrclibOptions
{
    public const string SectionName = "Lrclib";

    public string BaseUrl { get; set; } = "https://lrclib.net";

    public int RequestTimeoutSeconds { get; set; } = 15;

    public int MinRequestIntervalMilliseconds { get; set; } = 300;

    public string UserAgent { get; set; } = "Lyra.Enrichment.Worker/1.0";
}
