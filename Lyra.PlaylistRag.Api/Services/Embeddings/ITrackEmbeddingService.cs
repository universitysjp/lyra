namespace Lyra.PlaylistRag.Api.Services.Embeddings;

using Lyra.Contracts.Events;

public interface ITrackEmbeddingService
{
    Task UpsertAsync(SongEnrichedEvent enriched, CancellationToken ct);
}
