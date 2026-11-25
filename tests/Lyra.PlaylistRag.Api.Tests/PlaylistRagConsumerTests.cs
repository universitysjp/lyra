namespace Lyra.PlaylistRag.Api.Tests;

using Lyra.Contracts.Events;
using Lyra.Contracts.Music;
using Lyra.PlaylistRag.Api.Consumers;
using Lyra.PlaylistRag.Api.Data;
using Lyra.PlaylistRag.Api.Repositories;
using Lyra.PlaylistRag.Api.Services.Embeddings;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;
using Xunit;

public class PlaylistRagConsumerTests : IClassFixture<VectorPostgresTestFixture>
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);

    private readonly VectorPostgresTestFixture _fixture;

    public PlaylistRagConsumerTests(VectorPostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SongImportedConsumer_UpsertsTheUserTrackMapping()
    {
        await VectorTestDatabase.ResetAsync(_fixture.ConnectionString);
        var generator = new FakeTrackEmbeddingGenerator();
        await using var provider = CreateProvider(generator);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var userId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        var imported = new SongImportedEvent(
            trackId,
            userId,
            "local-1",
            "Local Title",
            "Local Artist",
            "Local Album",
            180,
            Now);

        await harness.Bus.Publish(imported);
        await WaitUntilAsync(() => harness.Consumed.Select<SongImportedEvent>().Any());

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VectorDbContext>();
        var row = await db.UserTracks.SingleAsync(link => link.UserId == userId && link.TrackId == trackId);

        Assert.Equal("local-1", row.LocalTrackId);
        Assert.Equal("Local Title", row.Title);
        Assert.Equal("Local Artist", row.Artist);
        Assert.Equal("Local Album", row.Album);
        Assert.Equal(180, row.DurationSeconds);
    }

    [Fact]
    public async Task SongImportedConsumer_DuplicateDeliveryIsIdempotent()
    {
        await VectorTestDatabase.ResetAsync(_fixture.ConnectionString);
        var generator = new FakeTrackEmbeddingGenerator();
        await using var provider = CreateProvider(generator);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var userId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        var imported = new SongImportedEvent(
            trackId,
            userId,
            "local-1",
            "Local Title",
            "Local Artist",
            "Local Album",
            180,
            Now);

        await harness.Bus.Publish(imported);
        await harness.Bus.Publish(imported);
        await WaitUntilAsync(() => harness.Consumed.Select<SongImportedEvent>().Count() >= 2);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VectorDbContext>();
        Assert.Equal(1, await db.UserTracks.CountAsync());
    }

    [Fact]
    public async Task SongEnrichedConsumer_StoresTheGlobalEmbedding()
    {
        await VectorTestDatabase.ResetAsync(_fixture.ConnectionString);
        var generator = new FakeTrackEmbeddingGenerator();
        await using var provider = CreateProvider(generator);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var userId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        var enriched = new SongEnrichedEvent(
            trackId,
            userId,
            "plain lyrics",
            "synced lyrics",
            "calm",
            ["night", "rain"],
            3,
            "A quiet, rainy track.",
            Now,
            new ResolvedTrackMetadata("Resolved Title", "Resolved Artist", "Resolved Album", 214, false));

        await harness.Bus.Publish(enriched);
        await WaitUntilAsync(() => harness.Consumed.Select<SongEnrichedEvent>().Any());

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VectorDbContext>();
        var row = await db.TrackEmbeddings.SingleAsync(embedding => embedding.TrackId == trackId);

        Assert.Equal("Resolved Title", row.Title);
        Assert.Equal("Resolved Artist", row.Artist);
        Assert.Equal("Resolved Album", row.Album);
        Assert.Equal(214, row.DurationSeconds);
        Assert.Equal("calm", row.PrimaryMood);
        Assert.Equal(["night", "rain"], row.Themes);
        Assert.Equal(3, row.EnergyLevel);
        Assert.Equal("A quiet, rainy track.", row.SemanticSummary);
        Assert.Equal("test-embedding", row.EmbeddingModel);
        Assert.Equal(Lyra.PlaylistRag.Api.Models.TrackEmbedding.Dimensions, row.EmbeddingDimensions);
        Assert.Contains("Mood: calm", generator.LastText);
        Assert.Contains("Themes: night, rain", generator.LastText);
        Assert.Contains("Summary: A quiet, rainy track.", generator.LastText);
    }

    [Fact]
    public async Task SongEnrichedConsumer_UsesLocalMetadataWhenResolvedMetadataIsMissing()
    {
        await VectorTestDatabase.ResetAsync(_fixture.ConnectionString);
        var generator = new FakeTrackEmbeddingGenerator();
        await using var provider = CreateProvider(generator);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var userId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        await harness.Bus.Publish(new SongImportedEvent(
            trackId,
            userId,
            "local-1",
            "Local Title",
            "Local Artist",
            "Local Album",
            180,
            Now));
        await WaitUntilAsync(() => harness.Consumed.Select<SongImportedEvent>().Any());

        await harness.Bus.Publish(new SongEnrichedEvent(
            trackId,
            userId,
            null,
            null,
            "calm",
            ["night"],
            2,
            "A calm local track.",
            Now));
        await WaitUntilAsync(() => harness.Consumed.Select<SongEnrichedEvent>().Any());

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VectorDbContext>();
        var row = await db.TrackEmbeddings.SingleAsync(embedding => embedding.TrackId == trackId);

        Assert.Equal("Local Title", row.Title);
        Assert.Equal("Local Artist", row.Artist);
        Assert.Equal("Local Album", row.Album);
        Assert.Equal(180, row.DurationSeconds);
    }

    [Fact]
    public async Task SongEnrichedConsumer_DuplicateDeliveryIsIdempotent()
    {
        await VectorTestDatabase.ResetAsync(_fixture.ConnectionString);
        var generator = new FakeTrackEmbeddingGenerator();
        await using var provider = CreateProvider(generator);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var enriched = new SongEnrichedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "lyrics",
            null,
            "energetic",
            ["dance"],
            8,
            "An energetic track.",
            Now,
            new ResolvedTrackMetadata("Title", "Artist", "Album", 200, false));

        await harness.Bus.Publish(enriched);
        await harness.Bus.Publish(enriched);
        await WaitUntilAsync(() => harness.Consumed.Select<SongEnrichedEvent>().Count() >= 2);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VectorDbContext>();
        Assert.Equal(1, await db.TrackEmbeddings.CountAsync());
    }

    private ServiceProvider CreateProvider(FakeTrackEmbeddingGenerator generator)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddDbContext<VectorDbContext>(options =>
            options.UseNpgsql(_fixture.ConnectionString, npgsql => npgsql.UseVector()));
        services.AddScoped<IUserTrackRepository, UserTrackRepository>();
        services.AddScoped<ITrackEmbeddingRepository, TrackEmbeddingRepository>();
        services.AddScoped<ITrackEmbeddingService, TrackEmbeddingService>();
        services.AddSingleton<ITrackEmbeddingGenerator>(generator);
        services.AddMassTransitTestHarness(config =>
        {
            config.AddConsumer<SongImportedConsumer>();
            config.AddConsumer<SongEnrichedConsumer>();
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
}
