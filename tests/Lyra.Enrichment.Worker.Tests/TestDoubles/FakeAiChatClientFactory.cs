namespace Lyra.Enrichment.Worker.Tests.TestDoubles;

using Lyra.AI;
using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;

internal sealed class FakeAiChatClientFactory(FakeChatClient client) : IAiChatClientFactory
{
    public AiConnectionSecret? Connection { get; private set; }

    public string? SessionId { get; private set; }

    public IChatClient Create(AiConnectionSecret connection, string? sessionId = null)
    {
        Connection = connection;
        SessionId = sessionId;
        return client;
    }
}
