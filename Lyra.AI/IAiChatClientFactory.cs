namespace Lyra.AI;

using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;

/// <summary>Creates a provider-neutral, bounded chat client for one request scope.</summary>
public interface IAiChatClientFactory
{
    IChatClient Create(AiConnectionSecret connection, string? sessionId = null);
}
