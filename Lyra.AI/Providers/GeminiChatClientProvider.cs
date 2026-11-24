namespace Lyra.AI.Providers;

using Google.GenAI;
using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;

internal sealed class GeminiChatClientProvider : IAiChatClientProvider
{
    public AiProvider Provider => AiProvider.Gemini;

    public IChatClient Create(AiConnectionSecret connection, string? sessionId)
    {
        var client = new Client(apiKey: connection.ApiKey, vertexAI: false);
        return client.AsIChatClient(connection.Model);
    }
}
