namespace Lyra.Identity.Api.Grpc;

using global::Grpc.Core;
using global::Grpc.Core.Interceptors;
using Lyra.Contracts.AI;
using Lyra.Identity.Api.Services.AiConnections;

public sealed class InternalServiceAuthenticationInterceptor(
    IInternalServiceAuthenticator authenticator) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var providedKey = context.RequestHeaders
            .FirstOrDefault(entry => string.Equals(
                entry.Key,
                InternalGrpcMetadata.ApiKeyHeader,
                StringComparison.OrdinalIgnoreCase))
            ?.Value;

        if (!authenticator.IsValid(providedKey))
        {
            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "An internal service credential is required."));
        }

        return await continuation(request, context);
    }
}
