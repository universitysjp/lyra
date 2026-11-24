namespace Lyra.Enrichment.Worker.Tests.TestDoubles;

using Lyra.Enrichment.Worker.Services.Lyrics;

internal sealed class FakeLyricsClient(
    Func<string, string, string?, int, CancellationToken, Task<LyricsResult>> handler)
    : ILyricsClient
{
    public int Calls { get; private set; }

    public Task<LyricsResult> GetLyricsAsync(
        string title,
        string artist,
        string? album,
        int durationSeconds,
        CancellationToken ct)
    {
        Calls++;
        return handler(title, artist, album, durationSeconds, ct);
    }
}
