namespace Lyra.Enrichment.Worker.Services.Lyrics;

public interface ILrclibRequestThrottle
{
    Task WaitAsync(CancellationToken ct);
}
