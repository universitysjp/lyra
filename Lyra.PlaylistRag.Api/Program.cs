using System.Security.Claims;
using DotNetEnv;
using Lyra.Contracts.Authentication;
using Lyra.PlaylistRag.Api.Configuration;
using Lyra.PlaylistRag.Api.Consumers;
using Lyra.PlaylistRag.Api.Data;
using Lyra.PlaylistRag.Api.Repositories;
using Lyra.PlaylistRag.Api.Services.Embeddings;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

// Load local secrets (connection strings, broker credentials) from the repository-root .env file.
Env.Load(options: new LoadOptions().TraversePath());

var builder = WebApplication.CreateBuilder(args);

// 1. Application configuration
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<PlaylistRagMessagingOptions>(
    builder.Configuration.GetSection(PlaylistRagMessagingOptions.SectionName));

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
builder.Services.AddSingleton<ITrackEmbeddingGenerator, UnconfiguredTrackEmbeddingGenerator>();

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

// 4. Web API
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

app.Run();

public partial class Program
{
}
