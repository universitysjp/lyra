namespace Lyra.Enrichment.Worker.Services.Enrichment;

using Lyra.Contracts.Events;
using MassTransit;

public sealed class MassTransitLyricsFetchedPublisher(IBus bus) : ILyricsFetchedPublisher
{
    public Task PublishAsync(SongLyricsFetchedEvent lyrics, CancellationToken ct) =>
        bus.Publish(lyrics, ct);
}
