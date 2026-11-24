using DotNetEnv;
using Grpc.Net.Client;
using Lyra.AI;
using Lyra.AI.Configuration;
using Lyra.Enrichment.Worker.Configuration;
using Lyra.Enrichment.Worker.Consumers;
using Lyra.Enrichment.Worker.Services.AiConnections;
using Lyra.Enrichment.Worker.Services.Caching;
using Lyra.Enrichment.Worker.Services.Enrichment;
using Lyra.Enrichment.Worker.Services.Lyrics;
using MassTransit;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using IdentityGrpcClient = Lyra.Contracts.Grpc.Identity.UserAiConnectionService.UserAiConnectionServiceClient;

// Load local secrets (connection strings, broker credentials) from the repository-root .env
// file in local development only; production reads injected environment variables only.
var environmentName =
    Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
if (string.IsNullOrEmpty(environmentName)
    || environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase))
{
    Env.Load(options: new LoadOptions().TraversePath());
}

var builder = Host.CreateApplicationBuilder(args);

// 1. Application configuration
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<WorkerMessagingOptions>(
    builder.Configuration.GetSection(WorkerMessagingOptions.SectionName));
builder.Services.Configure<IdentityOptions>(
    builder.Configuration.GetSection(IdentityOptions.SectionName));
builder.Services.Configure<InternalServicesOptions>(
    builder.Configuration.GetSection(InternalServicesOptions.SectionName));
builder.Services.Configure<LrclibOptions>(
    builder.Configuration.GetSection(LrclibOptions.SectionName));
builder.Services.Configure<EnrichmentOptions>(
    builder.Configuration.GetSection(EnrichmentOptions.SectionName));
builder.Services.Configure<AiProviderOptions>(
    builder.Configuration.GetSection(AiProviderOptions.SectionName));

var rabbitMq = builder.Configuration
    .GetSection(RabbitMqOptions.SectionName)
    .Get<RabbitMqOptions>() ?? new RabbitMqOptions();
var workerOptions = builder.Configuration
    .GetSection(WorkerMessagingOptions.SectionName)
    .Get<WorkerMessagingOptions>() ?? new WorkerMessagingOptions();
var identityOptions = builder.Configuration
    .GetSection(IdentityOptions.SectionName)
    .Get<IdentityOptions>() ?? new IdentityOptions();
var internalServices = builder.Configuration
    .GetSection(InternalServicesOptions.SectionName)
    .Get<InternalServicesOptions>() ?? new InternalServicesOptions();
var lrclibOptions = builder.Configuration
    .GetSection(LrclibOptions.SectionName)
    .Get<LrclibOptions>() ?? new LrclibOptions();
var enrichmentOptions = builder.Configuration
    .GetSection(EnrichmentOptions.SectionName)
    .Get<EnrichmentOptions>() ?? new EnrichmentOptions();

if (string.IsNullOrWhiteSpace(rabbitMq.Username)
    || string.IsNullOrWhiteSpace(rabbitMq.Password))
{
    throw new InvalidOperationException(
        "RabbitMQ credentials are missing. Set 'RabbitMq__Username' and 'RabbitMq__Password' " +
        "in the repository-root .env file (see .env.example).");
}

if (rabbitMq.Port is < 1 or > 65535)
{
    throw new InvalidOperationException("RabbitMq:Port must be between 1 and 65535.");
}

if (!builder.Environment.IsDevelopment()
    && !builder.Environment.IsEnvironment("Testing")
    && string.IsNullOrWhiteSpace(internalServices.ApiKey))
{
    throw new InvalidOperationException(
        "The internal gRPC credential is missing. Set 'InternalServices__ApiKey' " +
        "in the deployed environment (see .env.example for the expected value shape).");
}

if (!Uri.TryCreate(identityOptions.GrpcAddress, UriKind.Absolute, out var identityGrpcUri))
{
    throw new InvalidOperationException(
        "Identity:GrpcAddress must be an absolute gRPC endpoint URL.");
}

if (!builder.Environment.IsDevelopment()
    && identityGrpcUri.Scheme != Uri.UriSchemeHttps)
{
    throw new InvalidOperationException(
        "Identity:GrpcAddress must use HTTPS outside Development.");
}

if (identityGrpcUri.Scheme == Uri.UriSchemeHttp)
{
    // Required for loopback h2c in local development.
    AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
}

if (workerOptions.PrefetchCount <= 0
    || workerOptions.ConcurrentMessageLimit <= 0
    || workerOptions.RetryCount < 0
    || workerOptions.RetryInitialIntervalSeconds <= 0
    || workerOptions.RetryMaxIntervalSeconds < workerOptions.RetryInitialIntervalSeconds)
{
    throw new InvalidOperationException("Worker messaging options are invalid.");
}

if (lrclibOptions.RequestTimeoutSeconds <= 0
    || lrclibOptions.MinRequestIntervalMilliseconds < 0
    || lrclibOptions.MaxFuzzyCandidates <= 0
    || lrclibOptions.MaxCandidateDetailFetches <= 0
    || lrclibOptions.FuzzyMatchThreshold is <= 0 or > 100
    || lrclibOptions.DurationToleranceSeconds <= 0
    || lrclibOptions.SuccessCacheMinutes < 0
    || lrclibOptions.NotFoundCacheMinutes < 0
    || string.IsNullOrWhiteSpace(lrclibOptions.UserAgent))
{
    throw new InvalidOperationException("LRCLIB options are invalid.");
}

if (identityOptions.RequestTimeoutSeconds <= 0)
{
    throw new InvalidOperationException("Identity gRPC request timeout must be positive.");
}

if (enrichmentOptions.ResultCacheTtl <= TimeSpan.Zero
    || enrichmentOptions.LockExpiry <= TimeSpan.Zero
    || enrichmentOptions.MaxLyricsCharacters <= 0)
{
    throw new InvalidOperationException("Enrichment cache, lock, and prompt limits are invalid.");
}

var redisConnectionString =
    builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException(
        "Redis connection string is missing. Set 'ConnectionStrings__Redis' in .env.");

// 2. Messaging
builder.Services.AddMassTransit(bus =>
{
    bus.AddConsumer<SongImportedConsumer>();

    bus.AddConfigureEndpointsCallback((context, _, endpoint) =>
    {
        endpoint.UseMessageRetry(retry => retry.Exponential(
            workerOptions.RetryCount,
            TimeSpan.FromSeconds(workerOptions.RetryInitialIntervalSeconds),
            TimeSpan.FromSeconds(workerOptions.RetryMaxIntervalSeconds),
            TimeSpan.FromSeconds(workerOptions.RetryInitialIntervalSeconds)));
        endpoint.UseInMemoryOutbox(context);
        endpoint.ConcurrentMessageLimit = workerOptions.ConcurrentMessageLimit;
    });

    bus.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitMq.Host, checked((ushort)rabbitMq.Port), rabbitMq.VirtualHost, host =>
        {
            host.Username(rabbitMq.Username);
            host.Password(rabbitMq.Password);
        });

        cfg.PrefetchCount = checked((ushort)workerOptions.PrefetchCount);
        cfg.ConfigureEndpoints(context);
    });
});

// 3. Private Identity gRPC client
builder.Services.AddSingleton(_ =>
    GrpcChannel.ForAddress(identityGrpcUri));
builder.Services.AddSingleton(serviceProvider =>
    new IdentityGrpcClient(serviceProvider.GetRequiredService<GrpcChannel>()));
builder.Services.AddSingleton<IUserAiConnectionClient, IdentityUserAiConnectionClient>();

// 4. Enrichment pipeline
builder.Services.AddScoped<ITrackAnalysisService, TrackAnalysisService>();
builder.Services.AddScoped<ITrackEnrichmentService, TrackEnrichmentService>();
builder.Services.AddScoped<ILyricsFetchedPublisher, MassTransitLyricsFetchedPublisher>();

// 5. External services and caching
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddSingleton<IEnrichmentResultStore, RedisEnrichmentResultStore>();
builder.Services.AddSingleton<ILrclibRequestThrottle, LrclibRequestThrottle>();
builder.Services.AddHttpClient<ILyricsClient, LrclibLyricsClient>((serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<LrclibOptions>>().Value;
    httpClient.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    httpClient.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
});

// 6. Provider-neutral AI clients (created per user request)
builder.Services.AddLyraAiProviders();

var host = builder.Build();
await host.RunAsync();

public partial class Program
{
}
