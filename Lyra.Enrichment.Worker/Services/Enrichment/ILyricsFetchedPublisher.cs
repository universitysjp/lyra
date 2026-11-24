namespace Lyra.Enrichment.Worker.Services.Enrichment;

using Lyra.Contracts.Events;

/// <summary>
/// Publishes lyrics immediately, outside the consumer's in-memory outbox, so a later
/// AI failure does not discard lyrics that were already retrieved successfully.
/// </summary>
public interface ILyricsFetchedPublisher
{
    Task PublishAsync(SongLyricsFetchedEvent lyrics, CancellationToken ct);
}
