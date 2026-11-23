namespace Lyra.Enrichment.Worker.Configuration;

public sealed class WorkerMessagingOptions
{
    public const string SectionName = "Worker";

    public int PrefetchCount { get; set; } = 3;

    public int ConcurrentMessageLimit { get; set; } = 3;

    public int RetryCount { get; set; } = 5;

    public int RetryInitialIntervalSeconds { get; set; } = 2;

    public int RetryMaxIntervalSeconds { get; set; } = 30;
}
