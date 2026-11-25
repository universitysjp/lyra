namespace Lyra.PlaylistRag.Api.Services.Embeddings;

using Lyra.PlaylistRag.Api.Models;

/// <summary>
/// Temporary Step 2 binding. The embedding-provider step replaces this with the
/// configured deployment-wide generator.
/// </summary>
public sealed class UnconfiguredTrackEmbeddingGenerator : ITrackEmbeddingGenerator
{
    public string Model => "unconfigured";

    public int Dimensions => TrackEmbedding.Dimensions;

    public Task<ReadOnlyMemory<float>> GenerateAsync(string text, CancellationToken ct) =>
        throw new InvalidOperationException(
            "The embedding provider is not configured. Complete the embedding-provider setup before consuming enriched tracks.");
}
