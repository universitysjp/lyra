namespace Lyra.Enrichment.Worker.Tests.TestDoubles;

using Lyra.Contracts.Events;
using Lyra.Enrichment.Worker.Services.Enrichment;

internal sealed class FakeLyricsFetchedPublisher : ILyricsFetchedPublisher
{
    public List<SongLyricsFetchedEvent> Published { get; } = [];

    public Task PublishAsync(SongLyricsFetchedEvent lyrics, CancellationToken ct)
    {
        Published.Add(lyrics);
        return Task.CompletedTask;
    }
}
