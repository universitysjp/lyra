namespace Lyra.AI.Providers;

using Anthropic;
using Lyra.AI.Configuration;
using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

internal sealed class ClaudeChatClientProvider(
    IOptions<AiProviderOptions> options) : IAiChatClientProvider
{
    private readonly AiProviderOptions _options = options.Value;

    public AiProvider Provider => AiProvider.Anthropic;

    public IChatClient Create(AiConnectionSecret connection, string? sessionId)
    {
        var client = new AnthropicClient
        {
            ApiKey = connection.ApiKey,
            MaxRetries = 0
        };

        return client.AsIChatClient(connection.Model, _options.MaxOutputTokens);
    }
}
