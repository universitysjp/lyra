namespace Lyra.PlaylistRag.Api.Services.Embeddings;

using Lyra.AI;
using Lyra.Contracts.AI;
using Lyra.PlaylistRag.Api.Configuration;
using Lyra.PlaylistRag.Api.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

public sealed class AiTrackEmbeddingGenerator(
    IAiEmbeddingGeneratorFactory factory,
    IOptions<RagEmbeddingOptions>? options = null) : ITrackEmbeddingGenerator
{
    private readonly RagEmbeddingOptions _options = options?.Value ?? new RagEmbeddingOptions();

    public string Model => _options.Model;

    public int Dimensions => _options.Dimensions;

    public async Task<ReadOnlyMemory<float>> GenerateAsync(
        string text,
        string sessionId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Embedding text cannot be empty.", nameof(text));
        }

        if (text.Length > _options.MaxInputCharacters)
        {
            throw new ArgumentException(
                $"Embedding text cannot exceed {_options.MaxInputCharacters} characters.",
                nameof(text));
        }

        if (_options.Dimensions != TrackEmbedding.Dimensions)
        {
            throw new InvalidOperationException(
                $"Rag:Dimensions is {_options.Dimensions}, but vector_db expects {TrackEmbedding.Dimensions}.");
        }

        var provider = ParseProvider(_options.Provider);
        OpenCodePlan? plan = provider == AiProvider.OpenCode
            ? ParsePlan(_options.OpenCodePlan)
            : null;
        var connection = new AiConnectionSecret(
            provider,
            _options.Model,
            _options.ApiKey,
            plan);

        using var generator = factory.Create(connection, sessionId, _options.Dimensions);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));

        var result = await generator.GenerateAsync(
            [text],
            cancellationToken: timeout.Token);

        var vector = result[0].Vector;
        if (vector.Length != _options.Dimensions)
        {
            throw new InvalidOperationException(
                $"The embedding provider returned {vector.Length} dimensions, but {_options.Dimensions} were expected.");
        }

        return vector;
    }

    private static AiProvider ParseProvider(string value)
    {
        if (!Enum.TryParse<AiProvider>(value, ignoreCase: true, out var provider)
            || !Enum.IsDefined(provider))
        {
            throw new InvalidOperationException($"Rag:Provider '{value}' is not a supported AI provider.");
        }

        if (provider is not (AiProvider.OpenAI or AiProvider.OpenCode))
        {
            throw new InvalidOperationException(
                $"Rag:Provider '{value}' does not support embeddings in this build.");
        }

        return provider;
    }

    private static OpenCodePlan ParsePlan(string value)
    {
        if (!Enum.TryParse<OpenCodePlan>(value, ignoreCase: true, out var plan)
            || !Enum.IsDefined(plan))
        {
            throw new InvalidOperationException(
                $"Rag:OpenCodePlan '{value}' is invalid. Use 'Go' or 'PayAsYouGo'.");
        }

        return plan;
    }
}
