using System.Security.Claims;
using DotNetEnv;
using Grpc.Net.Client;
using Lyra.AI;
using Lyra.AI.Configuration;
using Lyra.AI.Identity;
using Lyra.Contracts.AI;
using Lyra.Contracts.Authentication;
using Lyra.PlaylistRag.Api.Configuration;
using Lyra.PlaylistRag.Api.Consumers;
using Lyra.PlaylistRag.Api.Data;
using Lyra.PlaylistRag.Api.Endpoints;
using Lyra.PlaylistRag.Api.Middleware;
using Lyra.PlaylistRag.Api.Models;
using Lyra.PlaylistRag.Api.Repositories;
using Lyra.PlaylistRag.Api.Services.Embeddings;
using Lyra.PlaylistRag.Api.Services.Playlists;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using IdentityGrpcClient = Lyra.Contracts.Grpc.Identity.UserAiConnectionService.UserAiConnectionServiceClient;

// Load local secrets (connection strings, broker credentials) from the repository-root .env file.
Env.Load(options: new LoadOptions().TraversePath());

var builder = WebApplication.CreateBuilder(args);

// 1. Application configuration
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<PlaylistRagMessagingOptions>(
    builder.Configuration.GetSection(PlaylistRagMessagingOptions.SectionName));
builder.Services.Configure<RagEmbeddingOptions>(
    builder.Configuration.GetSection(RagEmbeddingOptions.SectionName));
builder.Services.Configure<IdentityOptions>(
    builder.Configuration.GetSection(IdentityOptions.SectionName));
builder.Services.Configure<InternalServicesOptions>(
    builder.Configuration.GetSection(InternalServicesOptions.SectionName));
builder.Services.Configure<AiProviderOptions>(
    builder.Configuration.GetSection(AiProviderOptions.SectionName));
builder.Services.Configure<PlaylistOptions>(
    builder.Configuration.GetSection(PlaylistOptions.SectionName));

var vectorConnectionString =
    builder.Configuration.GetConnectionString("VectorDb")
    ?? throw new InvalidOperationException(
        "The vector database connection string is missing. Set 'ConnectionStrings__VectorDb' in the repository-root .env file.");

var rabbitMq = builder.Configuration
    .GetSection(RabbitMqOptions.SectionName)
    .Get<RabbitMqOptions>() ?? new RabbitMqOptions();
var messaging = builder.Configuration
    .GetSection(PlaylistRagMessagingOptions.SectionName)
    .Get<PlaylistRagMessagingOptions>() ?? new PlaylistRagMessagingOptions();
var ragEmbedding = builder.Configuration
    .GetSection(RagEmbeddingOptions.SectionName)
    .Get<RagEmbeddingOptions>() ?? new RagEmbeddingOptions();
var identityOptions = builder.Configuration
    .GetSection(IdentityOptions.SectionName)
    .Get<IdentityOptions>() ?? new IdentityOptions();
var internalServices = builder.Configuration
    .GetSection(InternalServicesOptions.SectionName)
    .Get<InternalServicesOptions>() ?? new InternalServicesOptions();
var playlistOptions = builder.Configuration
    .GetSection(PlaylistOptions.SectionName)
    .Get<PlaylistOptions>() ?? new PlaylistOptions();

if (!Enum.TryParse<AiProvider>(ragEmbedding.Provider, ignoreCase: true, out var embeddingProvider)
    || !Enum.IsDefined(embeddingProvider)
    || embeddingProvider is not (AiProvider.OpenAI or AiProvider.OpenCode))
{
    throw new InvalidOperationException(
        "Rag:Provider must be OpenAI or OpenCode for embeddings.");
}

if (embeddingProvider == AiProvider.OpenCode
    && (!Enum.TryParse<OpenCodePlan>(ragEmbedding.OpenCodePlan, ignoreCase: true, out var openCodePlan)
        || !Enum.IsDefined(openCodePlan)))
{
    throw new InvalidOperationException(
        "Rag:OpenCodePlan must be Go or PayAsYouGo for the OpenCode provider.");
}

if (string.IsNullOrWhiteSpace(ragEmbedding.Model)
    || string.IsNullOrWhiteSpace(ragEmbedding.ApiKey)
    || ragEmbedding.Dimensions != TrackEmbedding.Dimensions
    || ragEmbedding.MaxInputCharacters <= 0
    || ragEmbedding.RequestTimeoutSeconds <= 0)
{
    throw new InvalidOperationException(
        "Rag embedding configuration is invalid. Set Rag:Model, Rag:Dimensions (1536), " +
        "Rag:MaxInputCharacters, Rag:RequestTimeoutSeconds, and Rag__ApiKey.");
}

if (string.IsNullOrWhiteSpace(internalServices.ApiKey))
{
    throw new InvalidOperationException(
        "The internal gRPC credential is missing. Set 'InternalServices__ApiKey' in the repository-root .env file.");
}

if (!Uri.TryCreate(identityOptions.GrpcAddress, UriKind.Absolute, out var identityGrpcUri))
{
    throw new InvalidOperationException(
        "Identity:GrpcAddress must be an absolute gRPC endpoint URL.");
}

if (!builder.Environment.IsDevelopment()
    && !builder.Environment.IsEnvironment("Testing")
    && identityGrpcUri.Scheme != Uri.UriSchemeHttps)
{
    throw new InvalidOperationException(
        "Identity:GrpcAddress must use HTTPS outside Development.");
}

if (identityGrpcUri.Scheme == Uri.UriSchemeHttp)
{
    // Required for loopback h2c in local development and endpoint tests.
    AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
}

if (identityOptions.RequestTimeoutSeconds <= 0)
{
    throw new InvalidOperationException("Identity gRPC request timeout must be positive.");
}

if (playlistOptions.DefaultLimit < 1
    || playlistOptions.MaxLimit < playlistOptions.DefaultLimit
    || playlistOptions.MaxPromptCharacters <= 0
    || playlistOptions.MaxSemanticQueryCharacters <= 0
    || playlistOptions.MaxTitleCharacters <= 0
    || playlistOptions.MaxDescriptionCharacters <= 0
    || playlistOptions.MaxMoodCount < 0
    || playlistOptions.MaxThemeCount < 0
    || playlistOptions.MaxFilterValueCharacters <= 0
    || playlistOptions.MaxCandidatePool < playlistOptions.MaxLimit
    || playlistOptions.CandidatePoolMultiplier < 1
    || playlistOptions.MaxCandidatesPerArtist < 1
    || playlistOptions.MaxReasonCharacters <= 0)
{
    throw new InvalidOperationException("Playlist options are invalid.");
}

if (!builder.Environment.IsEnvironment("Testing")
    && (string.IsNullOrWhiteSpace(rabbitMq.Username)
        || string.IsNullOrWhiteSpace(rabbitMq.Password)))
{
    throw new InvalidOperationException(
        "RabbitMQ credentials are missing. Set 'RabbitMq__Username' and 'RabbitMq__Password' in the repository-root .env file.");
}

if (rabbitMq.Port is < 1 or > 65535
    || messaging.PrefetchCount <= 0
    || messaging.ConcurrentMessageLimit <= 0
    || messaging.RetryCount < 0
    || messaging.RetryInitialIntervalSeconds <= 0
    || messaging.RetryMaxIntervalSeconds < messaging.RetryInitialIntervalSeconds)
{
    throw new InvalidOperationException("PlaylistRag messaging options are invalid.");
}

// 2. Database
builder.Services.AddDbContext<VectorDbContext>(options =>
    options.UseNpgsql(vectorConnectionString, npgsql => npgsql.UseVector()));

// 3. Event ingestion
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IUserTrackRepository, UserTrackRepository>();
builder.Services.AddScoped<ITrackEmbeddingRepository, TrackEmbeddingRepository>();
builder.Services.AddScoped<ITrackEmbeddingService, TrackEmbeddingService>();
builder.Services.AddLyraAiProviders();
builder.Services.AddSingleton<ITrackEmbeddingGenerator, AiTrackEmbeddingGenerator>();

// 4. Private Identity gRPC client and playlist generation pipeline
builder.Services.AddSingleton(_ => GrpcChannel.ForAddress(identityGrpcUri));
builder.Services.AddSingleton(serviceProvider =>
    new IdentityGrpcClient(serviceProvider.GetRequiredService<GrpcChannel>()));
builder.Services.AddSingleton<IUserAiConnectionClient, IdentityUserAiConnectionClient>();
builder.Services.AddScoped<IPlaylistQueryPlanner, PlaylistQueryPlanner>();
builder.Services.AddScoped<IPlaylistCandidateSearch, PgvectorPlaylistCandidateSearch>();
builder.Services.AddScoped<IPlaylistSelector, PlaylistSelector>();
builder.Services.AddScoped<IPlaylistGenerationService, PlaylistGenerationService>();

builder.Services.AddMassTransit(bus =>
{
    bus.AddConsumer<SongImportedConsumer>()
        .Endpoint(endpoint => endpoint.Name = "playlist-rag-song-imported");
    bus.AddConsumer<SongEnrichedConsumer>()
        .Endpoint(endpoint => endpoint.Name = "playlist-rag-song-enriched");

    bus.AddConfigureEndpointsCallback((context, _, endpoint) =>
    {
        endpoint.UseMessageRetry(retry => retry.Exponential(
            messaging.RetryCount,
            TimeSpan.FromSeconds(messaging.RetryInitialIntervalSeconds),
            TimeSpan.FromSeconds(messaging.RetryMaxIntervalSeconds),
            TimeSpan.FromSeconds(messaging.RetryInitialIntervalSeconds)));
        endpoint.ConcurrentMessageLimit = messaging.ConcurrentMessageLimit;
    });

    if (builder.Environment.IsEnvironment("Testing"))
    {
        bus.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
    }
    else
    {
        bus.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(rabbitMq.Host, checked((ushort)rabbitMq.Port), rabbitMq.VirtualHost, host =>
            {
                host.Username(rabbitMq.Username);
                host.Password(rabbitMq.Password);
            });

            cfg.PrefetchCount = checked((ushort)messaging.PrefetchCount);
            cfg.ConfigureEndpoints(context);
        });
    }
});

// 5. Web API
builder.Services.AddOpenApi();
builder.Services.AddLyraJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<VectorDbContext>();
    await database.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<PlaylistExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/playlists/ping", (ClaimsPrincipal user) =>
{
    var userId = user.GetUserId();
    return TypedResults.Ok(new { message = "pong", userId });
})
.RequireAuthorization()
.WithName("PlaylistsPing")
.WithSummary("Echo authenticated user ID to verify token propagation");

app.MapPlaylistEndpoints();

app.Run();

public partial class Program
{
}
