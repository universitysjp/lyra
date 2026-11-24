namespace Lyra.Enrichment.Worker.Services.Lyrics;

public interface ILyricsClient
{
    Task<LyricsResult> GetLyricsAsync(
        string title,
        string artist,
        string? album,
        int durationSeconds,
        CancellationToken ct);
}
