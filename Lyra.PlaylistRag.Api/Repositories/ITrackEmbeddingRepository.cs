namespace Lyra.PlaylistRag.Api.Repositories;

using Lyra.PlaylistRag.Api.Models;

public interface ITrackEmbeddingRepository
{
    Task UpsertAsync(TrackEmbedding embedding, CancellationToken ct);
}
