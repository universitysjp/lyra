namespace Lyra.AI.Providers;

using System.ClientModel;
using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;
using OpenAI;

internal sealed class OpenAiChatClientProvider : IAiChatClientProvider
{
    public AiProvider Provider => AiProvider.OpenAI;

    public IChatClient Create(AiConnectionSecret connection, string? sessionId)
    {
        var client = new OpenAIClient(new ApiKeyCredential(connection.ApiKey));
        return client.GetChatClient(connection.Model).AsIChatClient();
    }
}
