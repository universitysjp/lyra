namespace Lyra.Enrichment.Worker.Tests.TestDoubles;

using Lyra.Enrichment.Worker.Services.Lyrics;

internal sealed class FakeLyricsClient(
    Func<string, string, string?, int, CancellationToken, Task<string?>> handler)
    : ILyricsClient
{
    public int Calls { get; private set; }

    public Task<string?> GetPlainLyricsAsync(
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
