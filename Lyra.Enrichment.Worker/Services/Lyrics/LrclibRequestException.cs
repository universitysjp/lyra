namespace Lyra.Enrichment.Worker.Services.Lyrics;

public sealed class LrclibRequestException(
    int? statusCode,
    bool isTransient,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public int? StatusCode { get; } = statusCode;

    public bool IsTransient { get; } = isTransient;
}
