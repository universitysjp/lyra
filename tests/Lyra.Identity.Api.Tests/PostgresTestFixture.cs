namespace Lyra.Identity.Api.Tests;

using DotNetEnv;
using Testcontainers.PostgreSql;
using Xunit;

public class PostgresTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }
        catch
        {
            // Fall back to a local PostgreSQL instance configured via the repository-root .env.
            Env.Load(options: new LoadOptions().TraversePath());
            ConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__IdentityDb")
                ?? throw new InvalidOperationException(
                    "Docker is unavailable and no 'ConnectionStrings__IdentityDb' is set in the environment or .env file.");
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            await _container.DisposeAsync();
        }
        catch
        {
        }
    }
}
