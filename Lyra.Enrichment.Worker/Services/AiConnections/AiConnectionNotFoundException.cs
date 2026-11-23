namespace Lyra.Enrichment.Worker.Services.AiConnections;

public sealed class AiConnectionNotFoundException(Guid userId)
    : Exception($"No AI provider connection is configured for user {userId}.");
