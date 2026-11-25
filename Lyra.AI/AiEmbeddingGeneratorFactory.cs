namespace Lyra.AI;

using Lyra.AI.Configuration;
using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

public sealed class AiEmbeddingGeneratorFactory : IAiEmbeddingGeneratorFactory
{
    private readonly Dictionary<AiProvider, IAiEmbeddingGeneratorProvider> _providers;
    private readonly AiProviderOptions _options;

    public AiEmbeddingGeneratorFactory(
        IEnumerable<IAiEmbeddingGeneratorProvider> providers,
        IOptions<AiProviderOptions>? options = null)
    {
        _providers = providers.ToDictionary(provider => provider.Provider);
        _options = options?.Value ?? new AiProviderOptions();
    }

    public IEmbeddingGenerator<string, Embedding<float>> Create(
        AiConnectionSecret connection,
        string? sessionId,
        int expectedDimensions)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ValidateOptions();
        ValidateConnection(connection, sessionId);

        if (expectedDimensions <= 0 || expectedDimensions > 8192)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedDimensions),
                "The expected embedding dimension must be between 1 and 8192.");
        }

        if (!_providers.TryGetValue(connection.Provider, out var provider))
        {
            throw new NotSupportedException(
                $"The {connection.Provider} provider does not support embeddings in this build.");
        }

        return provider.Create(connection, sessionId);
    }

    private void ValidateConnection(AiConnectionSecret connection, string? sessionId)
    {
        if (!Enum.IsDefined(connection.Provider))
        {
            throw new ArgumentOutOfRangeException(
                nameof(connection),
                connection.Provider,
                "The requested AI provider is not supported.");
        }

        if (string.IsNullOrWhiteSpace(connection.Model)
            || connection.Model.Length > _options.MaxModelLength)
        {
            throw new ArgumentException(
                $"The embedding model must be nonblank and no longer than {_options.MaxModelLength} characters.",
                nameof(connection));
        }

        if (string.IsNullOrWhiteSpace(connection.ApiKey)
            || connection.ApiKey.Length > _options.MaxApiKeyLength)
        {
            throw new ArgumentException(
                $"The API key must be nonblank and no longer than {_options.MaxApiKeyLength} characters.",
                nameof(connection));
        }

        if (connection.Provider == AiProvider.OpenCode)
        {
            if (connection.OpenCodePlan is null || !Enum.IsDefined(connection.OpenCodePlan.Value))
            {
                throw new ArgumentException(
                    "An OpenCode plan is required for the OpenCode provider.",
                    nameof(connection));
            }

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException(
                    "A session identifier is required for the OpenCode provider.",
                    nameof(sessionId));
            }
        }
        else if (connection.OpenCodePlan is not null)
        {
            throw new ArgumentException(
                "An OpenCode plan is only valid for the OpenCode provider.",
                nameof(connection));
        }
    }

    private void ValidateOptions()
    {
        if (_options.MaxModelLength <= 0 || _options.MaxApiKeyLength <= 0)
        {
            throw new InvalidOperationException(
                "AI provider input limits must be greater than zero.");
        }
    }
}
