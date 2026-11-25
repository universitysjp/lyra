namespace Lyra.AI.Identity;

public sealed class AiConnectionNotFoundException(Guid userId)
    : Exception($"No AI provider connection is configured for user {userId}.");
