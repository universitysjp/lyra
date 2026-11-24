namespace Lyra.Enrichment.Worker.Services.Lyrics;

public sealed class LrclibRequestException(
    int? statusCode,
    bool isTransient,
    string message,
    Exception? innerException = null,
    TimeSpan? retryAfter = null) : Exception(message, innerException)
{
    public int? StatusCode { get; } = statusCode;

    public bool IsTransient { get; } = isTransient;

    /// <summary>Server-provided retry delay, currently populated for HTTP 429.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
