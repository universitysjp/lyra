namespace Lyra.Enrichment.Worker.Services.AiConnections;

using global::Grpc.Core;

public sealed class IdentityServiceException(
    StatusCode statusCode,
    bool isTransient,
    string message) : Exception(message)
{
    public StatusCode StatusCode { get; } = statusCode;

    public bool IsTransient { get; } = isTransient;
}
