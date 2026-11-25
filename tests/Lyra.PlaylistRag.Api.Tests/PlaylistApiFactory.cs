namespace Lyra.PlaylistRag.Api.Tests;

using Lyra.PlaylistRag.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Pgvector.EntityFrameworkCore;

public sealed class PlaylistApiFactory(
    string connectionString,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:VectorDb", connectionString);
        builder.UseSetting("Rag:Provider", "OpenAI");
        builder.UseSetting("Rag:Model", "text-embedding-3-small");
        builder.UseSetting("Rag:Dimensions", "1536");
        builder.UseSetting("Rag:ApiKey", "test-rag-key");
        builder.UseSetting("Rag:MaxInputCharacters", "8000");
        builder.UseSetting("Rag:RequestTimeoutSeconds", "60");
        builder.UseSetting("Identity:GrpcAddress", "http://localhost:5004");
        builder.UseSetting("Identity:RequestTimeoutSeconds", "10");
        builder.UseSetting("InternalServices:ApiKey", "test-internal-key");
        builder.UseSetting("Jwt:MetadataAddress", "http://localhost:5001/.well-known/openid-configuration");
        builder.UseSetting("RabbitMq:Username", "test");
        builder.UseSetting("RabbitMq:Password", "test");
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:VectorDb"] = connectionString,
                ["Rag:Provider"] = "OpenAI",
                ["Rag:Model"] = "text-embedding-3-small",
                ["Rag:Dimensions"] = "1536",
                ["Rag:ApiKey"] = "test-rag-key",
                ["Rag:MaxInputCharacters"] = "8000",
                ["Rag:RequestTimeoutSeconds"] = "60",
                ["Identity:GrpcAddress"] = "http://localhost:5004",
                ["Identity:RequestTimeoutSeconds"] = "10",
                ["InternalServices:ApiKey"] = "test-internal-key",
                ["Jwt:MetadataAddress"] = "http://localhost:5001/.well-known/openid-configuration",
                ["RabbitMq:Username"] = "test",
                ["RabbitMq:Password"] = "test"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<VectorDbContext>>();
            services.AddDbContext<VectorDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            configureServices?.Invoke(services);
        });
    }
}
