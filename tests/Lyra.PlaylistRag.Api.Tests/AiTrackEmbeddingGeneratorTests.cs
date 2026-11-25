namespace Lyra.PlaylistRag.Api.Tests;

using Lyra.AI;
using Lyra.Contracts.AI;
using Lyra.PlaylistRag.Api.Configuration;
using Lyra.PlaylistRag.Api.Models;
using Lyra.PlaylistRag.Api.Services.Embeddings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Xunit;

public class AiTrackEmbeddingGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_UsesFactorySessionAndDimensions()
    {
        var factory = new FakeEmbeddingFactory(TrackEmbedding.Dimensions);
        var generator = new AiTrackEmbeddingGenerator(
            factory,
            Options.Create(CreateOptions()));

        var vector = await generator.GenerateAsync("hello", "session-123", CancellationToken.None);

        Assert.Equal(TrackEmbedding.Dimensions, vector.Length);
        Assert.Equal("session-123", factory.LastSessionId);
        Assert.Equal(TrackEmbedding.Dimensions, factory.LastExpectedDimensions);
        Assert.Equal(AiProvider.OpenCode, factory.LastConnection?.Provider);
        Assert.Equal(OpenCodePlan.Go, factory.LastConnection?.OpenCodePlan);
    }

    [Fact]
    public async Task GenerateAsync_RejectsProviderDimensionMismatch()
    {
        var factory = new FakeEmbeddingFactory(TrackEmbedding.Dimensions - 1);
        var generator = new AiTrackEmbeddingGenerator(
            factory,
            Options.Create(CreateOptions()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateAsync("hello", "session-123", CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_RejectsOversizedText()
    {
        var factory = new FakeEmbeddingFactory(TrackEmbedding.Dimensions);
        var options = CreateOptions();
        options.MaxInputCharacters = 5;
        var generator = new AiTrackEmbeddingGenerator(factory, Options.Create(options));

        await Assert.ThrowsAsync<ArgumentException>(
            () => generator.GenerateAsync("too long", "session-123", CancellationToken.None));
    }

    private static RagEmbeddingOptions CreateOptions() =>
        new()
        {
            Provider = "OpenCode",
            Model = "text-embedding-3-small",
            Dimensions = TrackEmbedding.Dimensions,
            OpenCodePlan = "Go",
            ApiKey = "oc_test_key",
            MaxInputCharacters = 8000,
            RequestTimeoutSeconds = 5
        };

    private sealed class FakeEmbeddingFactory(int dimensions) : IAiEmbeddingGeneratorFactory
    {
        public AiConnectionSecret? LastConnection { get; private set; }

        public string? LastSessionId { get; private set; }

        public int LastExpectedDimensions { get; private set; }

        public IEmbeddingGenerator<string, Embedding<float>> Create(
            AiConnectionSecret connection,
            string? sessionId,
            int expectedDimensions)
        {
            LastConnection = connection;
            LastSessionId = sessionId;
            LastExpectedDimensions = expectedDimensions;

            return new FakeEmbeddingGenerator(dimensions);
        }
    }

    private sealed class FakeEmbeddingGenerator(int dimensions)
        : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var embeddings = values
                .Select(_ => new Embedding<float>(new float[dimensions]))
                .ToList();

            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
