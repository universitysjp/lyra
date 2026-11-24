namespace Lyra.Enrichment.Worker.Tests;

using Lyra.Contracts.Events;
using Lyra.Enrichment.Worker.Consumers;
using Lyra.Enrichment.Worker.Services.Enrichment;
using Lyra.Enrichment.Worker.Tests.TestDoubles;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class SongImportedConsumerTests
{
    [Fact]
    public async Task Consume_PublishesOneEnrichedEventWithOriginalIdentifiers()
    {
        var import = Import();
        var fakeService = new FakeTrackEnrichmentService((request, _, _) =>
            Task.FromResult(new SongEnrichedEvent(
                request.TrackId,
                request.UserId,
                "lyrics",
                "synced lyrics",
                "calm",
                ["ambient"],
                2,
                "summary",
                DateTimeOffset.UtcNow)));
        await using var provider = CreateProvider(fakeService);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(import);
        await WaitUntilAsync(
            () => harness.Published.Select<SongEnrichedEvent>().Any());

        var published = Assert.Single(harness.Published.Select<SongEnrichedEvent>());
        Assert.Equal(import.TrackId, published.Context.Message.TrackId);
        Assert.Equal(import.UserId, published.Context.Message.UserId);
        Assert.Equal("lyrics", published.Context.Message.Lyrics);
        Assert.Equal("synced lyrics", published.Context.Message.SyncedLyrics);
        Assert.Equal(1, fakeService.Calls);
        Assert.False(string.IsNullOrWhiteSpace(fakeService.LastSessionId));
    }

    [Fact]
    public async Task Consume_FailurePublishesNoEnrichedEvent()
    {
        var fakeService = new FakeTrackEnrichmentService((_, _, _) =>
            throw new InvalidOperationException("enrichment failed"));
        await using var provider = CreateProvider(fakeService);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(Import());
        await WaitUntilAsync(
            () => harness.Consumed.Select<SongImportedEvent>().Any(
                consumed => consumed.Exception is not null));

        Assert.Equal(1, fakeService.Calls);
        Assert.Empty(harness.Published.Select<SongEnrichedEvent>());
    }

    private static ServiceProvider CreateProvider(ITrackEnrichmentService enrichmentService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(enrichmentService);
        services.AddMassTransitTestHarness(config =>
        {
            config.AddConsumer<SongImportedConsumer>();
            config.AddConfigureEndpointsCallback((context, _, endpoint) =>
                endpoint.UseInMemoryOutbox(context));
        });

        return services.BuildServiceProvider(validateScopes: true);
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

        Assert.Fail("The expected message flow did not complete before the timeout.");
    }

    private static SongImportedEvent Import() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "local-1",
            "Song",
            "Artist",
            "Album",
            180,
            DateTimeOffset.UtcNow);
}
