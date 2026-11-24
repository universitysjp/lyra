using DotNetEnv;
using Lyra.Catalog.Api.Configuration;
using Lyra.Catalog.Api.Consumers;
using Lyra.Catalog.Api.Data;
using Lyra.Catalog.Api.Endpoints;
using Lyra.Catalog.Api.Middleware;
using Lyra.Catalog.Api.Repositories;
using Lyra.Catalog.Api.Services.Caching;
using Lyra.Catalog.Api.Services.Catalog;
using Lyra.Contracts.Authentication;
using MassTransit;
using Microsoft.EntityFrameworkCore;

// Load local secrets (connection strings, broker credentials) from the repository-root .env
// file in local development only; production reads injected environment variables only.
var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
if (string.IsNullOrEmpty(environmentName)
    || environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase))
{
    Env.Load(options: new LoadOptions().TraversePath());
}

var builder = WebApplication.CreateBuilder(args);

// 1. Application configuration
builder.Services.Configure<AppStringOptions>(
    builder.Configuration.GetSection(AppStringOptions.SectionName));
builder.Services.Configure<CatalogOptions>(
    builder.Configuration.GetSection(CatalogOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));

var appStrings = builder.Configuration.GetSection(AppStringOptions.SectionName).Get<AppStringOptions>()
    ?? new AppStringOptions();
var connectionString =
    builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException(
        string.IsNullOrWhiteSpace(appStrings.Errors.CatalogDbMissing)
            ? "CatalogDb connection string is missing."
            : appStrings.Errors.CatalogDbMissing);
var redisConnectionString =
    builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException(
        string.IsNullOrWhiteSpace(appStrings.Errors.RedisMissing)
            ? "Redis connection string is missing."
            : appStrings.Errors.RedisMissing);

// 2. Infrastructure and database
builder.Services.AddDbContext<CatalogDbContext>(
    options => options.UseNpgsql(connectionString));
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = redisConnectionString);

// 3. Messaging
var rabbitMq = builder.Configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
    ?? new RabbitMqOptions();

if (!builder.Environment.IsEnvironment("Testing") &&
    (string.IsNullOrWhiteSpace(rabbitMq.Username) || string.IsNullOrWhiteSpace(rabbitMq.Password)))
{
    throw new InvalidOperationException(
        "RabbitMQ credentials are missing. Set 'RabbitMq__Username' and 'RabbitMq__Password' in the repository-root .env file (see .env.example).");
}

builder.Services.AddMassTransit(bus =>
{
    bus.AddEntityFrameworkOutbox<CatalogDbContext>(outbox =>
    {
        outbox.UsePostgres();
        outbox.UseBusOutbox();
    });
    bus.AddConsumer<SongEnrichedConsumer>();
    bus.AddConsumer<SongLyricsFetchedConsumer>();
    bus.AddConfigureEndpointsCallback((context, _, endpoint) =>
        endpoint.UseEntityFrameworkOutbox<CatalogDbContext>(context));

    if (builder.Environment.IsEnvironment("Testing"))
    {
        bus.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
    }
    else
    {
        bus.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(rabbitMq.Host, rabbitMq.VirtualHost, host =>
            {
                host.Username(rabbitMq.Username);
                host.Password(rabbitMq.Password);
            });

            cfg.ConfigureEndpoints(context);
        });
    }
});

// 4. Application services
builder.Services.AddScoped<ITrackRepository, TrackRepository>();
builder.Services.AddScoped<ILibraryCache, LibraryCache>();
builder.Services.AddScoped<ITrackIngestionService, TrackIngestionService>();
builder.Services.AddScoped<ITrackQueryService, TrackQueryService>();
builder.Services.AddScoped<ITrackEnrichmentService, TrackEnrichmentService>();
builder.Services.AddScoped<ITrackLyricsService, TrackLyricsService>();

// 5. API configuration
builder.Services.AddOpenApi();

// 6. Security
builder.Services.AddLyraJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();

// 7. Database migrations (development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.MigrateAsync();

    app.MapOpenApi();
}

// 8. Middleware pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// 9. Endpoint registration
app.MapTrackEndpoints();

app.Run();

public partial class Program { }
