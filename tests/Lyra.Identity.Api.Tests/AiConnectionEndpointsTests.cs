namespace Lyra.Identity.Api.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using global::Grpc.Core;
using global::Grpc.Net.Client;
using Lyra.Contracts.AI;
using Lyra.Identity.Api.Data;
using Lyra.Identity.Api.DTOs.Requests;
using Lyra.Identity.Api.DTOs.Responses;
using Lyra.Identity.Api.Services.Authentication;
using GrpcAiProvider = Lyra.Contracts.Grpc.Identity.AiProvider;
using GrpcOpenCodePlan = Lyra.Contracts.Grpc.Identity.OpenCodePlan;
using GrpcResponse = Lyra.Contracts.Grpc.Identity.GetAiConnectionResponse;
using IdentityGrpcClient = Lyra.Contracts.Grpc.Identity.UserAiConnectionService.UserAiConnectionServiceClient;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class AiConnectionEndpointsTests : IClassFixture<PostgresTestFixture>
{
    private const string InternalKey = "test-internal-key-32-characters-long";
    private readonly PostgresTestFixture _fixture;

    public AiConnectionEndpointsTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Crud_EncryptsKeyAndReturnsMetadataOnly()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);
        var apiKey = $"sk-openai-{Guid.NewGuid():N}";

        var upsertResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            "/api/auth/ai-connection",
            auth.AccessToken,
            JsonContent.Create(new UpsertAiConnectionRequest(
                AiProvider.OpenAI,
                "gpt-test",
                apiKey,
                null)));

        Assert.Equal(HttpStatusCode.OK, upsertResponse.StatusCode);
        var upsertBody = await upsertResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(apiKey, upsertBody);
        Assert.DoesNotContain("apiKey", upsertBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("protectedApiKey", upsertBody, StringComparison.OrdinalIgnoreCase);

        var metadata = JsonSerializer.Deserialize<AiConnectionResponse>(upsertBody, JsonOptions());
        Assert.NotNull(metadata);
        Assert.Equal(AiProvider.OpenAI, metadata.Provider);
        Assert.Equal("gpt-test", metadata.Model);
        Assert.Null(metadata.OpenCodePlan);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var stored = await db.UserAiConnections
                .AsNoTracking()
                .SingleAsync(entry => entry.UserId == auth.UserId);

            Assert.NotEqual(apiKey, stored.ProtectedApiKey);
            Assert.DoesNotContain(apiKey, stored.ProtectedApiKey);
            Assert.Equal(AiProvider.OpenAI, stored.Provider);
        }

        var getResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/auth/ai-connection",
            auth.AccessToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getBody = await getResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(apiKey, getBody);
        var fetchedMetadata = JsonSerializer.Deserialize<AiConnectionResponse>(
            getBody,
            JsonOptions());
        Assert.NotNull(fetchedMetadata);

        var replacementKey = $"gemini-{Guid.NewGuid():N}";
        var replaceResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            "/api/auth/ai-connection",
            auth.AccessToken,
            JsonContent.Create(new UpsertAiConnectionRequest(
                AiProvider.Gemini,
                "gemini-test",
                replacementKey,
                null)));
        Assert.Equal(HttpStatusCode.OK, replaceResponse.StatusCode);

        var replaced = JsonSerializer.Deserialize<AiConnectionResponse>(
            await replaceResponse.Content.ReadAsStringAsync(),
            JsonOptions());
        Assert.NotNull(replaced);
        Assert.Equal(AiProvider.Gemini, replaced.Provider);
        Assert.Equal(fetchedMetadata.CreatedAt, replaced.CreatedAt);
        Assert.True(replaced.UpdatedAt >= fetchedMetadata.UpdatedAt);

        using var grpcChannel = CreateGrpcChannel(factory);
        var grpcClient = new IdentityGrpcClient(grpcChannel);
        var secret = await GetInternalSecretAsync(grpcClient, auth.UserId);

        Assert.Equal(GrpcAiProvider.Gemini, secret.Provider);
        Assert.Equal("gemini-test", secret.Model);
        Assert.Equal(replacementKey, secret.ApiKey);
        Assert.Equal(GrpcOpenCodePlan.Unspecified, secret.OpenCodePlan);

        var removedHttpEndpoint = await client.GetAsync(
            $"/internal/ai-connections/{auth.UserId}");
        Assert.Equal(HttpStatusCode.NotFound, removedHttpEndpoint.StatusCode);

        var deleteResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Delete,
            "/api/auth/ai-connection",
            auth.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var missingPublicResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/auth/ai-connection",
            auth.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, missingPublicResponse.StatusCode);

        var missingInternal = await Assert.ThrowsAsync<RpcException>(
            () => GetInternalSecretAsync(grpcClient, auth.UserId));
        Assert.Equal(StatusCode.NotFound, missingInternal.StatusCode);

        var repeatedDeleteResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Delete,
            "/api/auth/ai-connection",
            auth.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, repeatedDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task Users_CanOnlyManageTheirOwnConnection()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var userA = await RegisterAsync(client);
        var userB = await RegisterAsync(client);
        var keyA = $"sk-a-{Guid.NewGuid():N}";
        var keyB = $"sk-b-{Guid.NewGuid():N}";

        var putA = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            "/api/auth/ai-connection",
            userA.AccessToken,
            JsonContent.Create(new UpsertAiConnectionRequest(
                AiProvider.OpenAI,
                "model-a",
                keyA,
                null)));
        Assert.Equal(HttpStatusCode.OK, putA.StatusCode);

        var getB = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/auth/ai-connection",
            userB.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, getB.StatusCode);

        var putB = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            "/api/auth/ai-connection",
            userB.AccessToken,
            JsonContent.Create(new UpsertAiConnectionRequest(
                AiProvider.OpenCode,
                "model-b",
                keyB,
                OpenCodePlan.Go)));
        Assert.Equal(HttpStatusCode.OK, putB.StatusCode);

        using var grpcChannel = CreateGrpcChannel(factory);
        var grpcClient = new IdentityGrpcClient(grpcChannel);
        var secretA = await GetInternalSecretAsync(grpcClient, userA.UserId);
        var secretB = await GetInternalSecretAsync(grpcClient, userB.UserId);
        Assert.Equal(keyA, secretA.ApiKey);
        Assert.Equal(keyB, secretB.ApiKey);
        Assert.Equal(GrpcAiProvider.OpenAi, secretA.Provider);
        Assert.Equal(GrpcAiProvider.OpenCode, secretB.Provider);
        Assert.Equal(GrpcOpenCodePlan.Go, secretB.OpenCodePlan);

        var deleteA = await SendAuthorizedAsync(
            client,
            HttpMethod.Delete,
            "/api/auth/ai-connection",
            userA.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteA.StatusCode);

        var secretBAfterDelete = await GetInternalSecretAsync(grpcClient, userB.UserId);
        Assert.Equal(keyB, secretBAfterDelete.ApiKey);
    }

    [Fact]
    public async Task PublicEndpoints_RequireJwt()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/auth/ai-connection")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PutAsJsonAsync(
                "/api/auth/ai-connection",
                new UpsertAiConnectionRequest(AiProvider.OpenAI, "model", "key", null))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.DeleteAsync("/api/auth/ai-connection")).StatusCode);
    }

    [Fact]
    public async Task InternalGrpc_RejectsMissingWrongOrUserCredentials()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);
        await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            "/api/auth/ai-connection",
            auth.AccessToken,
            JsonContent.Create(new UpsertAiConnectionRequest(
                AiProvider.Anthropic,
                "claude-test",
                "anthropic-secret",
                null)));

        using var grpcChannel = CreateGrpcChannel(factory);
        var grpcClient = new IdentityGrpcClient(grpcChannel);

        var missingKey = await Assert.ThrowsAsync<RpcException>(
            () => GetInternalSecretAsync(grpcClient, auth.UserId, internalKey: null));
        Assert.Equal(StatusCode.Unauthenticated, missingKey.StatusCode);

        var wrongKey = await Assert.ThrowsAsync<RpcException>(
            () => GetInternalSecretAsync(grpcClient, auth.UserId, "wrong-internal-key"));
        Assert.Equal(StatusCode.Unauthenticated, wrongKey.StatusCode);

        var userJwtOnly = await Assert.ThrowsAsync<RpcException>(
            () => GetInternalSecretAsync(
                grpcClient,
                auth.UserId,
                internalKey: null,
                bearerToken: auth.AccessToken));
        Assert.Equal(StatusCode.Unauthenticated, userJwtOnly.StatusCode);

        var invalidUserId = await Assert.ThrowsAsync<RpcException>(
            async () => await grpcClient.GetAiConnectionAsync(new()
            {
                UserId = "not-a-guid"
            }, Headers(InternalKey)));
        Assert.Equal(StatusCode.InvalidArgument, invalidUserId.StatusCode);

        var correctKey = await GetInternalSecretAsync(grpcClient, auth.UserId);
        Assert.Equal(GrpcAiProvider.Anthropic, correctKey.Provider);
        Assert.Equal("anthropic-secret", correctKey.ApiKey);
    }

    [Theory]
    [InlineData(AiProvider.OpenAI, "", "key", null)]
    [InlineData(AiProvider.OpenAI, "model", "", null)]
    [InlineData(AiProvider.OpenAI, "model", "key", OpenCodePlan.Go)]
    [InlineData(AiProvider.OpenCode, "model", "key", null)]
    public async Task Put_InvalidProviderConnection_ReturnsBadRequest(
        AiProvider provider,
        string model,
        string apiKey,
        OpenCodePlan? plan)
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);

        var response = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            "/api/auth/ai-connection",
            auth.AccessToken,
            JsonContent.Create(new UpsertAiConnectionRequest(
                provider,
                model,
                apiKey,
                plan)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_ApiKeyAboveConfiguredBound_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);

        var response = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            "/api/auth/ai-connection",
            auth.AccessToken,
            JsonContent.Create(new UpsertAiConnectionRequest(
                AiProvider.OpenAI,
                "model",
                new string('x', 513),
                null)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StoredKey_CanBeDecryptedAfterHostRecreation()
    {
        var apiKey = $"sk-persisted-{Guid.NewGuid():N}";
        Guid userId;

        using (var firstFactory = CreateFactory())
        {
            var client = firstFactory.CreateClient();
            var auth = await RegisterAsync(client);
            userId = auth.UserId;

            var putResponse = await SendAuthorizedAsync(
                client,
                HttpMethod.Put,
                "/api/auth/ai-connection",
                auth.AccessToken,
                JsonContent.Create(new UpsertAiConnectionRequest(
                    AiProvider.OpenAI,
                    "persisted-model",
                    apiKey,
                    null)));
            Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        }

        using var secondFactory = CreateFactory();
        using var grpcChannel = CreateGrpcChannel(secondFactory);
        var grpcClient = new IdentityGrpcClient(grpcChannel);
        var secret = await GetInternalSecretAsync(grpcClient, userId);

        Assert.Equal("persisted-model", secret.Model);
        Assert.Equal(apiKey, secret.ApiKey);
    }

    [Fact]
    public async Task OpenApi_DoesNotExposeInternalConnectionEndpoint()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var document = await client.GetStringAsync("/openapi/v1.json");

        Assert.DoesNotContain("/internal/ai-connections", document);
        Assert.DoesNotContain("UserAiConnectionService", document);
        Assert.Contains("/api/auth/ai-connection", document);
    }

    [Fact]
    public async Task MissingPublicConnection_ReturnsNotFoundProblem()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);

        var response = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/auth/ai-connection",
            auth.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            // Tests own the database container; do not depend on a local .env file.
            builder.UseSetting("ConnectionStrings:IdentityDb", _fixture.ConnectionString);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["InternalServices:ApiKey"] = InternalKey
                });
            });

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<IdentityDbContext>));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<IdentityDbContext>(options =>
                    options.UseNpgsql(_fixture.ConnectionString));

                services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                    .Configure<IRsaKeyProvider>((options, keyProvider) =>
                    {
                        options.TokenValidationParameters.IssuerSigningKey =
                            keyProvider.GetPublicJsonWebKey();
                    });
            });
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Database.Migrate();

        return factory;
    }

    private static async Task<AuthResponse> RegisterAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                $"ai_{Guid.NewGuid():N}@example.com",
                "StrongPassword123!"));

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string accessToken,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private static GrpcChannel CreateGrpcChannel(WebApplicationFactory<Program> factory) =>
        GrpcChannel.ForAddress(factory.Server.BaseAddress, new GrpcChannelOptions
        {
            HttpHandler = factory.Server.CreateHandler()
        });

    private static async Task<GrpcResponse> GetInternalSecretAsync(
        IdentityGrpcClient client,
        Guid userId,
        string? internalKey = InternalKey,
        string? bearerToken = null)
    {
        var response = await client.GetAiConnectionAsync(
            new() { UserId = userId.ToString() },
            Headers(internalKey, bearerToken));

        return response;
    }

    private static Metadata Headers(
        string? internalKey,
        string? bearerToken = null)
    {
        var headers = new Metadata();
        if (internalKey is not null)
        {
            headers.Add(InternalGrpcMetadata.ApiKeyHeader, internalKey);
        }

        if (bearerToken is not null)
        {
            headers.Add("authorization", $"Bearer {bearerToken}");
        }

        return headers;
    }

    private static JsonSerializerOptions JsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<AiProvider>());
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<OpenCodePlan>());
        return options;
    }
}
