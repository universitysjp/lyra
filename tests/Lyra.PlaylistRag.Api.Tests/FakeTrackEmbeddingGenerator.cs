namespace Lyra.PlaylistRag.Api.Tests;

using Lyra.PlaylistRag.Api.Models;
using Lyra.PlaylistRag.Api.Services.Embeddings;

internal sealed class FakeTrackEmbeddingGenerator : ITrackEmbeddingGenerator
{
    public string Model => "test-embedding";

    public int Dimensions => TrackEmbedding.Dimensions;

    public string? LastText { get; private set; }

    public Task<ReadOnlyMemory<float>> GenerateAsync(
        string text,
        string sessionId,
        CancellationToken ct)
    {
        LastText = text;

        var values = new float[Dimensions];
        values[0] = 1;
        return Task.FromResult<ReadOnlyMemory<float>>(values);
    }
}
