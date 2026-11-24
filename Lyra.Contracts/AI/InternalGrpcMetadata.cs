namespace Lyra.Contracts.AI;

/// <summary>Metadata keys used by private service-to-service gRPC calls.</summary>
public static class InternalGrpcMetadata
{
    public const string ApiKeyHeader = "x-lyra-internal-key";
}
