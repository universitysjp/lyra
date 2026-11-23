namespace Lyra.Enrichment.Worker.Services.AiConnections;

using global::Grpc.Core;
using Lyra.Contracts.AI;
using Lyra.Enrichment.Worker.Configuration;
using Microsoft.Extensions.Options;
using GrpcAiProvider = Lyra.Contracts.Grpc.Identity.AiProvider;
using GrpcOpenCodePlan = Lyra.Contracts.Grpc.Identity.OpenCodePlan;
using GrpcRequest = Lyra.Contracts.Grpc.Identity.GetAiConnectionRequest;
using IdentityGrpcClient = Lyra.Contracts.Grpc.Identity.UserAiConnectionService.UserAiConnectionServiceClient;

public sealed class IdentityUserAiConnectionClient(
    IdentityGrpcClient grpcClient,
    IOptions<IdentityOptions>? identityOptions = null,
    IOptions<InternalServicesOptions>? internalOptions = null) : IUserAiConnectionClient
{
    private readonly IdentityOptions _identity = identityOptions?.Value ?? new IdentityOptions();
    private readonly InternalServicesOptions _internal =
        internalOptions?.Value ?? new InternalServicesOptions();

    public async Task<AiConnectionSecret> GetRequiredAsync(
        Guid userId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_internal.ApiKey))
        {
            throw new IdentityServiceException(
                StatusCode.Unauthenticated,
                isTransient: false,
                "The internal service credential is not configured.");
        }

        var headers = new Metadata
        {
            { InternalGrpcMetadata.ApiKeyHeader, _internal.ApiKey }
        };

        var deadline = DateTime.UtcNow.AddSeconds(
            Math.Max(1, _identity.RequestTimeoutSeconds));

        try
        {
            var response = await grpcClient.GetAiConnectionAsync(
                new GrpcRequest { UserId = userId.ToString("D") },
                headers,
                deadline,
                ct);

            return Map(userId, response);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            throw new AiConnectionNotFoundException(userId);
        }
        catch (RpcException exception)
        {
            if (exception.StatusCode == StatusCode.Cancelled && ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }

            throw MapException(exception);
        }
    }

    private static AiConnectionSecret Map(
        Guid userId,
        Lyra.Contracts.Grpc.Identity.GetAiConnectionResponse response)
    {
        var provider = response.Provider switch
        {
            GrpcAiProvider.OpenAi => AiProvider.OpenAI,
            GrpcAiProvider.Gemini => AiProvider.Gemini,
            GrpcAiProvider.OpenCode => AiProvider.OpenCode,
            GrpcAiProvider.Anthropic => AiProvider.Anthropic,
            _ => throw InvalidResponse("The AI provider returned by Identity is invalid.")
        };

        OpenCodePlan? plan = response.OpenCodePlan switch
        {
            GrpcOpenCodePlan.Unspecified => null,
            GrpcOpenCodePlan.PayAsYouGo when provider == AiProvider.OpenCode =>
                OpenCodePlan.PayAsYouGo,
            GrpcOpenCodePlan.Go when provider == AiProvider.OpenCode =>
                OpenCodePlan.Go,
            _ => throw InvalidResponse("The OpenCode plan returned by Identity is invalid.")
        };

        if (string.IsNullOrWhiteSpace(response.Model)
            || string.IsNullOrWhiteSpace(response.ApiKey))
        {
            throw InvalidResponse("Identity returned an incomplete AI connection.");
        }

        if (provider == AiProvider.OpenCode && plan is null)
        {
            throw InvalidResponse("Identity omitted the OpenCode plan.");
        }

        return new AiConnectionSecret(provider, response.Model, response.ApiKey, plan);
    }

    private static IdentityServiceException MapException(RpcException exception)
    {
        var isTransient = exception.StatusCode is
            StatusCode.Unavailable or
            StatusCode.DeadlineExceeded or
            StatusCode.ResourceExhausted or
            StatusCode.Internal or
            StatusCode.Unknown or
            StatusCode.Aborted;

        return new IdentityServiceException(
            exception.StatusCode,
            isTransient,
            isTransient
                ? "The Identity gRPC service is temporarily unavailable."
                : "The Identity gRPC service rejected the request.");
    }

    private static IdentityServiceException InvalidResponse(string message) =>
        new(StatusCode.Internal, isTransient: false, message);
}
