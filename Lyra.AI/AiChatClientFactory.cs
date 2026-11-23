namespace Lyra.AI;

using Lyra.AI.Configuration;
using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

public sealed class AiChatClientFactory : IAiChatClientFactory
{
    private readonly Dictionary<AiProvider, IAiChatClientProvider> _providers;
    private readonly AiProviderOptions _options;

    public AiChatClientFactory(
        IEnumerable<IAiChatClientProvider> providers,
        IOptions<AiProviderOptions>? options = null)
    {
        _providers = providers.ToDictionary(provider => provider.Provider);
        _options = options?.Value ?? new AiProviderOptions();
    }

    public IChatClient Create(AiConnectionSecret connection, string? sessionId = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ValidateOptions();
        ValidateConnection(connection, sessionId);

        if (!_providers.TryGetValue(connection.Provider, out var provider))
        {
            throw new ArgumentOutOfRangeException(
                nameof(connection),
                connection.Provider,
                "The requested AI provider is not supported.");
        }

        var client = provider.Create(connection, sessionId);
        return new AiProviderChatClient(client, connection.Provider, _options);
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
                $"The model must be nonblank and no longer than {_options.MaxModelLength} characters.",
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
        if (_options.RequestTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("AI provider request timeout must be greater than zero.");
        }

        if (_options.MaxOutputTokens <= 0)
        {
            throw new InvalidOperationException("AI provider maximum output tokens must be greater than zero.");
        }

        if (_options.MaxModelLength <= 0 || _options.MaxApiKeyLength <= 0)
        {
            throw new InvalidOperationException("AI provider input limits must be greater than zero.");
        }
    }
}
