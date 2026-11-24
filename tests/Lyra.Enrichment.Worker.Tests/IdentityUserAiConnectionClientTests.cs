namespace Lyra.Enrichment.Worker.Tests;

using global::Grpc.Core;
using Lyra.Contracts.AI;
using Lyra.Enrichment.Worker.Configuration;
using Lyra.Enrichment.Worker.Services.AiConnections;
using Lyra.Enrichment.Worker.Tests.TestDoubles;
using Microsoft.Extensions.Options;
using Xunit;
using GrpcAiProvider = Lyra.Contracts.Grpc.Identity.AiProvider;
using GrpcOpenCodePlan = Lyra.Contracts.Grpc.Identity.OpenCodePlan;
using GrpcRequest = Lyra.Contracts.Grpc.Identity.GetAiConnectionRequest;
using GrpcResponse = Lyra.Contracts.Grpc.Identity.GetAiConnectionResponse;
using IdentityGrpcClient = Lyra.Contracts.Grpc.Identity.UserAiConnectionService.UserAiConnectionServiceClient;

public class IdentityUserAiConnectionClientTests
{
    private const string InternalKey = "test-internal-key-32-characters-long";
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task GetRequiredAsync_MapsResponseAndSendsCredentialMetadata()
    {
        var invoker = new FakeIdentityCallInvoker(_ => new GrpcResponse
        {
            Provider = GrpcAiProvider.OpenCode,
            Model = "opencode-test",
            ApiKey = "oc_secret",
            OpenCodePlan = GrpcOpenCodePlan.Go
        });
        var client = CreateClient(invoker);

        var secret = await client.GetRequiredAsync(UserId, CancellationToken.None);

        Assert.Equal(AiProvider.OpenCode, secret.Provider);
        Assert.Equal("opencode-test", secret.Model);
        Assert.Equal("oc_secret", secret.ApiKey);
        Assert.Equal(OpenCodePlan.Go, secret.OpenCodePlan);
        Assert.Equal(
            InternalKey,
            Assert.Single(
                invoker.Headers!,
                entry => entry.Key == InternalGrpcMetadata.ApiKeyHeader).Value);
        Assert.True(invoker.Deadline > DateTime.UtcNow);

        var request = Assert.IsType<GrpcRequest>(invoker.Request);
        Assert.Equal(UserId.ToString("D"), request.UserId);
    }

    [Fact]
    public async Task GetRequiredAsync_NotFoundMapsToMissingConnection()
    {
        var invoker = ErrorInvoker(StatusCode.NotFound);
        var client = CreateClient(invoker);

        await Assert.ThrowsAsync<AiConnectionNotFoundException>(
            () => client.GetRequiredAsync(UserId, CancellationToken.None));
    }

    [Theory]
    [InlineData(StatusCode.Unavailable, true)]
    [InlineData(StatusCode.DeadlineExceeded, true)]
    [InlineData(StatusCode.Internal, true)]
    [InlineData(StatusCode.Unauthenticated, false)]
    [InlineData(StatusCode.PermissionDenied, false)]
    [InlineData(StatusCode.InvalidArgument, false)]
    public async Task GetRequiredAsync_MapsRpcStatusForRetryDecisions(
        StatusCode status,
        bool expectedTransient)
    {
        var client = CreateClient(ErrorInvoker(status));

        var exception = await Assert.ThrowsAsync<IdentityServiceException>(
            () => client.GetRequiredAsync(UserId, CancellationToken.None));

        Assert.Equal(status, exception.StatusCode);
        Assert.Equal(expectedTransient, exception.IsTransient);
    }

    [Fact]
    public async Task GetRequiredAsync_MissingLocalCredentialFailsWithoutCallingIdentity()
    {
        var invoker = new FakeIdentityCallInvoker(_ => new GrpcResponse());
        var client = CreateClient(invoker, internalKey: string.Empty);

        var exception = await Assert.ThrowsAsync<IdentityServiceException>(
            () => client.GetRequiredAsync(UserId, CancellationToken.None));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        Assert.False(exception.IsTransient);
        Assert.Equal(0, invoker.CallCount);
    }

    [Fact]
    public async Task GetRequiredAsync_InvalidProviderResponseIsRejected()
    {
        var client = CreateClient(new FakeIdentityCallInvoker(_ => new GrpcResponse
        {
            Provider = GrpcAiProvider.Unspecified,
            Model = "model",
            ApiKey = "secret"
        }));

        var exception = await Assert.ThrowsAsync<IdentityServiceException>(
            () => client.GetRequiredAsync(UserId, CancellationToken.None));

        Assert.False(exception.IsTransient);
        Assert.Contains("provider", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRequiredAsync_OpenCodeWithoutPlanIsRejected()
    {
        var client = CreateClient(new FakeIdentityCallInvoker(_ => new GrpcResponse
        {
            Provider = GrpcAiProvider.OpenCode,
            Model = "model",
            ApiKey = "secret",
            OpenCodePlan = GrpcOpenCodePlan.Unspecified
        }));

        var exception = await Assert.ThrowsAsync<IdentityServiceException>(
            () => client.GetRequiredAsync(UserId, CancellationToken.None));

        Assert.False(exception.IsTransient);
        Assert.Contains("plan", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FakeIdentityCallInvoker ErrorInvoker(StatusCode status) =>
        new(_ => throw new RpcException(new Status(status, "provider detail")));

    private static IdentityUserAiConnectionClient CreateClient(
        FakeIdentityCallInvoker invoker,
        string internalKey = InternalKey) =>
        new(
            new IdentityGrpcClient(invoker),
            Options.Create(new IdentityOptions
            {
                GrpcAddress = "http://localhost:5004",
                RequestTimeoutSeconds = 10
            }),
            Options.Create(new InternalServicesOptions { ApiKey = internalKey }));
}
