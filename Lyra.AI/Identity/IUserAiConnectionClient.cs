namespace Lyra.AI.Identity;

using Lyra.Contracts.AI;

public interface IUserAiConnectionClient
{
    Task<AiConnectionSecret> GetRequiredAsync(Guid userId, CancellationToken ct);
}
