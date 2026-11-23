namespace Lyra.Identity.Api.Grpc;

using global::Grpc.Core;
using Lyra.Contracts.AI;
using Lyra.Identity.Api.Services.AiConnections;
using ContractAiProvider = Lyra.Contracts.AI.AiProvider;
using ContractOpenCodePlan = Lyra.Contracts.AI.OpenCodePlan;
using GrpcAiProvider = Lyra.Contracts.Grpc.Identity.AiProvider;
using GrpcOpenCodePlan = Lyra.Contracts.Grpc.Identity.OpenCodePlan;
using GrpcService = Lyra.Contracts.Grpc.Identity.UserAiConnectionService;

public sealed class UserAiConnectionGrpcService(
    IUserAiConnectionService service)
    : GrpcService.UserAiConnectionServiceBase
{
    public override async Task<Lyra.Contracts.Grpc.Identity.GetAiConnectionResponse> GetAiConnection(
        Lyra.Contracts.Grpc.Identity.GetAiConnectionRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId) || userId == Guid.Empty)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "A valid user id is required."));
        }

        var connection = await service.GetSecretAsync(userId, context.CancellationToken);
        if (connection is null)
        {
            throw new RpcException(new Status(
                StatusCode.NotFound,
                "No AI provider connection is configured for this user."));
        }

        return new Lyra.Contracts.Grpc.Identity.GetAiConnectionResponse
        {
            Provider = ToGrpc(connection.Provider),
            Model = connection.Model,
            ApiKey = connection.ApiKey,
            OpenCodePlan = ToGrpc(connection.OpenCodePlan)
        };
    }

    private static GrpcAiProvider ToGrpc(ContractAiProvider provider) => provider switch
    {
        ContractAiProvider.OpenAI => GrpcAiProvider.OpenAi,
        ContractAiProvider.Gemini => GrpcAiProvider.Gemini,
        ContractAiProvider.OpenCode => GrpcAiProvider.OpenCode,
        ContractAiProvider.Anthropic => GrpcAiProvider.Anthropic,
        _ => throw new RpcException(new Status(
            StatusCode.Internal,
            "The stored AI provider is invalid."))
    };

    private static GrpcOpenCodePlan ToGrpc(ContractOpenCodePlan? plan) => plan switch
    {
        null => GrpcOpenCodePlan.Unspecified,
        ContractOpenCodePlan.PayAsYouGo => GrpcOpenCodePlan.PayAsYouGo,
        ContractOpenCodePlan.Go => GrpcOpenCodePlan.Go,
        _ => throw new RpcException(new Status(
            StatusCode.Internal,
            "The stored OpenCode plan is invalid."))
    };
}
