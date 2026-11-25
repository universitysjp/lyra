namespace Lyra.PlaylistRag.Api.Tests;

using DotNetEnv;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

/// <summary>
/// Provides an isolated pgvector database per test class. Prefers Testcontainers; falls back to
/// LYRA_TEST_POSTGRES, then to ConnectionStrings__VectorDb from the repository-root .env file.
/// </summary>
public class VectorPostgresTestFixture : IAsyncLifetime
{
    private readonly string? _configuredConnectionString =
        Environment.GetEnvironmentVariable("LYRA_TEST_POSTGRES");

    private PostgreSqlContainer? _container;
    private string? _maintenanceConnectionString;
    private string? _databaseName;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_configuredConnectionString))
        {
            await CreateIsolatedDatabaseAsync(_configuredConnectionString);
            return;
        }

        try
        {
            _container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }
        catch
        {
            Env.Load(options: new LoadOptions().TraversePath());
            var fallback = Environment.GetEnvironmentVariable("ConnectionStrings__VectorDb")
                ?? throw new InvalidOperationException(
                    "Docker is unavailable and no 'ConnectionStrings__VectorDb' is set in the environment or .env file.");
            await CreateIsolatedDatabaseAsync(fallback);
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        if (_maintenanceConnectionString is null || _databaseName is null)
        {
            return;
        }

        try
        {
            NpgsqlConnection.ClearAllPools();

            await using var connection = new NpgsqlConnection(_maintenanceConnectionString);
            await connection.OpenAsync();

            await using (var terminate = connection.CreateCommand())
            {
                terminate.CommandText =
                    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
                    "WHERE datname = @database AND pid <> pg_backend_pid()";
                terminate.Parameters.AddWithValue("database", _databaseName);
                await terminate.ExecuteNonQueryAsync();
            }

            await using var drop = connection.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\"";
            await drop.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private async Task CreateIsolatedDatabaseAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        _maintenanceConnectionString = builder.ConnectionString;
        _databaseName = $"vector_test_{Guid.NewGuid():N}";

        await using var connection = new NpgsqlConnection(_maintenanceConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
        await command.ExecuteNonQueryAsync();

        var databaseBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = _databaseName
        };
        ConnectionString = databaseBuilder.ConnectionString;
    }
}
