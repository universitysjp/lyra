namespace Lyra.Enrichment.Worker.Services.Lyrics;

public interface ILrclibRequestThrottle
{
    Task WaitAsync(CancellationToken ct);

    /// <summary>Blocks subsequent requests until the server-provided retry delay elapses.</summary>
    void Pause(TimeSpan duration);
}
