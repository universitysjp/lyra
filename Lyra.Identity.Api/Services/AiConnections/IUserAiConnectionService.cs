namespace Lyra.Identity.Api.Services.AiConnections;

using Lyra.Contracts.AI;
using Lyra.Identity.Api.DTOs.Requests;
using Lyra.Identity.Api.DTOs.Responses;

public interface IUserAiConnectionService
{
    Task<AiConnectionResponse> GetAsync(Guid userId, CancellationToken ct);

    Task<AiConnectionResponse> UpsertAsync(
        Guid userId,
        UpsertAiConnectionRequest request,
        CancellationToken ct);

    Task DeleteAsync(Guid userId, CancellationToken ct);

    Task<AiConnectionSecret?> GetSecretAsync(Guid userId, CancellationToken ct);
}
