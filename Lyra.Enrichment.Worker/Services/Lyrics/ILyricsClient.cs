namespace Lyra.Enrichment.Worker.Services.Lyrics;

public interface ILyricsClient
{
    Task<string?> GetPlainLyricsAsync(
        string title,
        string artist,
        string? album,
        int durationSeconds,
        CancellationToken ct);
}
