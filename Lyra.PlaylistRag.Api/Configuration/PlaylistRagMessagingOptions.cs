namespace Lyra.PlaylistRag.Api.Configuration;

public sealed class PlaylistRagMessagingOptions
{
    public const string SectionName = "PlaylistRag";

    public int PrefetchCount { get; set; } = 3;

    public int ConcurrentMessageLimit { get; set; } = 3;

    public int RetryCount { get; set; } = 5;

    public int RetryInitialIntervalSeconds { get; set; } = 2;

    public int RetryMaxIntervalSeconds { get; set; } = 30;
}
