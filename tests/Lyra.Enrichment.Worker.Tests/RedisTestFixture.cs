namespace Lyra.Enrichment.Worker.Tests;

using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

public sealed class RedisTestFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7-alpine").Build();

    public IConnectionMultiplexer Redis { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Redis = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (Redis is not null)
        {
            await Redis.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}
