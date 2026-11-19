namespace Lyra.Catalog.Api.Tests;

using Lyra.Catalog.Api.Configuration;
using Lyra.Catalog.Api.Data;
using Lyra.Catalog.Api.DTOs.Requests;
using Lyra.Catalog.Api.Exceptions;
using Lyra.Catalog.Api.Repositories;
using Lyra.Catalog.Api.Services.Caching;
using Lyra.Catalog.Api.Services.Catalog;
using Lyra.Contracts.Events;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

public class TrackIngestionServiceTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture _fixture;

    public TrackIngestionServiceTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ServiceProvider> CreateProviderAsync()
    {
        await CatalogTestDatabase.ResetAsync(_fixture.ConnectionString);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<CatalogDbContext>(
            options => options.UseNpgsql(_fixture.ConnectionString));
        services.AddDistributedMemoryCache();
        services.Configure<CatalogOptions>(_ => { });
        services.Configure<AppStringOptions>(_ => { });

        services.AddScoped<ITrackRepository, TrackRepository>();
        services.AddScoped<ILibraryCache, LibraryCache>();
        services.AddScoped<ITrackIngestionService, TrackIngestionService>();

        services.AddMassTransitTestHarness();

        var provider = services.BuildServiceProvider(validateScopes: true);
        await provider.GetRequiredService<ITestHarness>().Start();

        return provider;
    }

    private static ImportTracksRequest CreateRequest(params (string LocalTrackId, string Title, string Artist)[] items) =>
        new(items
            .Select(item => new TrackMetadataItem(item.LocalTrackId, item.Title, item.Artist, null, 200))
            .ToList());

    private static async Task<(int Tracks, int Links)> CountRowsAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        return (await db.Tracks.CountAsync(), await db.UserTracks.CountAsync());
    }

    [Fact]
    public async Task ImportBatchAsync_NewTracks_LinksThemAndPublishesEvents()
    {
        await using var provider = await CreateProviderAsync();
        var harness = provider.GetRequiredService<ITestHarness>();
        var userId = Guid.NewGuid();

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITrackIngestionService>();

        var response = await service.ImportBatchAsync(
            userId,
            CreateRequest(
                ("local-1", "Bohemian Rhapsody", "Queen"),
                ("local-2", "Sultans of Swing", "Dire Straits"),
                ("local-3", "Don't Stop Me Now", "Queen")),
            default);

        var events = harness.Published.Select<SongImportedEvent>().ToList();
        var (trackCount, linkCount) = await CountRowsAsync(provider);

        Assert.Equal(3, response.Submitted);
        Assert.Equal(3, response.Imported);
        Assert.Equal(0, response.Duplicates);
        Assert.Equal(3, events.Count);
        Assert.Equal(3, trackCount);
        Assert.Equal(3, linkCount);
        Assert.All(events, published =>
        {
            Assert.Equal(userId, published.Context.Message.UserId);
            Assert.NotEqual(Guid.Empty, published.Context.Message.TrackId);
        });
        Assert.Equal(
            ["local-1", "local-2", "local-3"],
            events.Select(published => published.Context.Message.LocalTrackId).OrderBy(id => id));
    }

    [Fact]
    public async Task ImportBatchAsync_RepeatImport_IsIdempotentAndPublishesNothing()
    {
        await using var provider = await CreateProviderAsync();
        var harness = provider.GetRequiredService<ITestHarness>();
        var userId = Guid.NewGuid();

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITrackIngestionService>();

        var request = CreateRequest(
            ("local-1", "Bohemian Rhapsody", "Queen"),
            ("local-2", "Sultans of Swing", "Dire Straits"),
            ("local-3", "Don't Stop Me Now", "Queen"));

        var first = await service.ImportBatchAsync(userId, request, default);
        var second = await service.ImportBatchAsync(userId, request, default);

        var events = harness.Published.Select<SongImportedEvent>().ToList();
        var (trackCount, linkCount) = await CountRowsAsync(provider);

        Assert.Equal(3, first.Imported);
        Assert.Equal(0, second.Imported);
        Assert.Equal(3, second.Duplicates);
        Assert.Equal(3, events.Count);
        Assert.Equal(3, trackCount);
        Assert.Equal(3, linkCount);
    }

    [Fact]
    public async Task ImportBatchAsync_InBatchDuplicates_AreCollapsedToOneLinkAndOneEvent()
    {
        await using var provider = await CreateProviderAsync();
        var harness = provider.GetRequiredService<ITestHarness>();
        var userId = Guid.NewGuid();

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITrackIngestionService>();

        var response = await service.ImportBatchAsync(
            userId,
            CreateRequest(
                ("local-a", "Bohemian Rhapsody", "Queen"),
                ("local-a-copy", "bohemian  rhapsody", "QUEEN"),
                ("local-b", "Don't Stop Me Now", "Queen")),
            default);

        var events = harness.Published.Select<SongImportedEvent>().ToList();
        var (trackCount, linkCount) = await CountRowsAsync(provider);

        Assert.Equal(3, response.Submitted);
        Assert.Equal(2, response.Imported);
        Assert.Equal(1, response.Duplicates);
        Assert.Equal(2, events.Count);
        Assert.Equal(2, trackCount);
        Assert.Equal(2, linkCount);
    }

    [Fact]
    public async Task ImportBatchAsync_SecondUserReusesGlobalTracks()
    {
        await using var provider = await CreateProviderAsync();
        var harness = provider.GetRequiredService<ITestHarness>();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITrackIngestionService>();

        var request = CreateRequest(
            ("a-1", "Bohemian Rhapsody", "Queen"),
            ("a-2", "Sultans of Swing", "Dire Straits"),
            ("a-3", "Don't Stop Me Now", "Queen"));

        await service.ImportBatchAsync(userA, request, default);

        var userBRequest = CreateRequest(
            ("b-1", "Bohemian Rhapsody", "Queen"),
            ("b-2", "Sultans of Swing", "Dire Straits"),
            ("b-3", "Don't Stop Me Now", "Queen"));

        var response = await service.ImportBatchAsync(userB, userBRequest, default);

        var events = harness.Published.Select<SongImportedEvent>().ToList();
        var (trackCount, linkCount) = await CountRowsAsync(provider);

        Assert.Equal(3, response.Imported);
        Assert.Equal(0, response.Duplicates);
        Assert.Equal(6, events.Count);
        Assert.Equal(3, trackCount);
        Assert.Equal(6, linkCount);
        Assert.Equal(3, events.Count(published => published.Context.Message.UserId == userB));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task ImportBatchAsync_EmptyOrOversizedBatch_IsRejectedWithoutWrites(int itemCount)
    {
        await using var provider = await CreateProviderAsync();
        var harness = provider.GetRequiredService<ITestHarness>();
        var userId = Guid.NewGuid();

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITrackIngestionService>();

        var items = Enumerable.Range(0, itemCount)
            .Select(index => ($"local-{index}", $"Title {index}", "Artist"))
            .ToArray();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.ImportBatchAsync(userId, CreateRequest(items), default));

        var (trackCount, linkCount) = await CountRowsAsync(provider);

        Assert.Equal(0, trackCount);
        Assert.Equal(0, linkCount);
        Assert.False(await harness.Published.Any<SongImportedEvent>());
    }

    [Fact]
    public async Task ImportBatchAsync_MissingTitle_RejectsWholeBatch()
    {
        await using var provider = await CreateProviderAsync();
        var harness = provider.GetRequiredService<ITestHarness>();
        var userId = Guid.NewGuid();

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITrackIngestionService>();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.ImportBatchAsync(
                userId,
                CreateRequest(
                    ("valid", "Bohemian Rhapsody", "Queen"),
                    ("invalid", "   ", "Queen")),
                default));

        var (trackCount, linkCount) = await CountRowsAsync(provider);

        Assert.Equal(0, trackCount);
        Assert.Equal(0, linkCount);
        Assert.False(await harness.Published.Any<SongImportedEvent>());
    }
}
