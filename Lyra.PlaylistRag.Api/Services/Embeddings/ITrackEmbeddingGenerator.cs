namespace Lyra.PlaylistRag.Api.Services.Embeddings;

public interface ITrackEmbeddingGenerator
{
    string Model { get; }

    int Dimensions { get; }

    Task<ReadOnlyMemory<float>> GenerateAsync(
        string text,
        string sessionId,
        CancellationToken ct);
}
