namespace Lyra.AI;

using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;

/// <summary>Creates provider-specific clients from a user's connection.</summary>
public interface IAiChatClientProvider
{
    AiProvider Provider { get; }

    IChatClient Create(AiConnectionSecret connection, string? sessionId);
}
