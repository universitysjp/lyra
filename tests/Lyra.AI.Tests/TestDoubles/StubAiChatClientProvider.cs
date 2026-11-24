namespace Lyra.AI.Tests.TestDoubles;

using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;

internal sealed class StubAiChatClientProvider(
    AiProvider provider,
    FakeChatClient client) : IAiChatClientProvider
{
    public AiProvider Provider { get; } = provider;

    public IChatClient Create(AiConnectionSecret connection, string? sessionId) => client;
}
