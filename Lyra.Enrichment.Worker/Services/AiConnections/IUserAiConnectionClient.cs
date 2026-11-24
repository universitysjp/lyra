namespace Lyra.Enrichment.Worker.Services.AiConnections;

using Lyra.Contracts.AI;

public interface IUserAiConnectionClient
{
    Task<AiConnectionSecret> GetRequiredAsync(Guid userId, CancellationToken ct);
}
