namespace Lyra.Catalog.Api.Tests;

using Lyra.Catalog.Api.Consumers;
using Lyra.Catalog.Api.Services.Catalog;
using Lyra.Contracts.Events;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class SongEnrichedConsumerTests
{
    [Fact]
    public async Task Consume_AppliesEnrichmentFromTheMessage()
    {
        var service = new RecordingEnrichmentService();
        var services = new ServiceCollection();
        services.AddSingleton<ITrackEnrichmentService>(service);
        services.AddMassTransitTestHarness(config =>
            config.AddConsumer<SongEnrichedConsumer>());

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var enriched = new SongEnrichedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "lyrics",
            "synced lyrics",
            "calm",
            ["ambient"],
            2,
            "summary",
            DateTimeOffset.UtcNow);

        await harness.Bus.Publish(enriched);
        await WaitUntilAsync(() => service.Calls == 1);

        Assert.True(service.Applied);
        Assert.Equal(enriched.TrackId, service.LastEvent?.TrackId);
        Assert.Equal(enriched.UserId, service.LastEvent?.UserId);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("The consumer did not process the enriched event before the timeout.");
    }

    private sealed class RecordingEnrichmentService : ITrackEnrichmentService
    {
        public int Calls { get; private set; }

        public bool Applied { get; private set; }

        public SongEnrichedEvent? LastEvent { get; private set; }

        public Task<bool> ApplyAsync(SongEnrichedEvent enriched, CancellationToken ct)
        {
            Calls++;
            LastEvent = enriched;
            Applied = true;
            return Task.FromResult(true);
        }
    }
}
