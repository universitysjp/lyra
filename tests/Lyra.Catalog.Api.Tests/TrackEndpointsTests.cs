namespace Lyra.Catalog.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using Lyra.Catalog.Api.DTOs.Requests;
using Lyra.Catalog.Api.DTOs.Responses;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class TrackEndpointsTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture _fixture;

    public TrackEndpointsTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<CatalogApiFactory> CreateFactoryAsync()
    {
        await CatalogTestDatabase.ResetAsync(_fixture.ConnectionString);
        return new CatalogApiFactory(_fixture.ConnectionString);
    }

    private static HttpClient CreateClient(CatalogApiFactory factory, Guid? userId = null)
    {
        var client = factory.CreateClient();

        if (userId is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.Value.ToString());
        }

        return client;
    }

    private static ImportTracksRequest CreateRequest(
        params (string LocalTrackId, string Title, string Artist, int DurationSeconds)[] items) =>
        new(items
            .Select(item => new TrackMetadataItem(
                item.LocalTrackId, item.Title, item.Artist, null, item.DurationSeconds))
            .ToList());

    private static ImportTracksRequest DefaultRequest() => CreateRequest(
        ("local-1", "Bohemian Rhapsody", "Queen", 355),
        ("local-2", "Sultans of Swing", "Dire Straits", 348),
        ("local-3", "Don't Stop Me Now", "Queen", 209));

    [Fact]
    public async Task Batch_Then_List_Then_Search_ReturnsTheCallersLibrary()
    {
        using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory, Guid.NewGuid());

        var importResponse = await client.PostAsJsonAsync(
            "/api/catalog/tracks/batch", DefaultRequest());
        var imported = await importResponse.Content.ReadFromJsonAsync<ImportTracksResponse>();

        Assert.Equal(HttpStatusCode.Accepted, importResponse.StatusCode);
        Assert.NotNull(imported);
        Assert.Equal(3, imported.Submitted);
        Assert.Equal(3, imported.Imported);
        Assert.Equal(0, imported.Duplicates);

        var page = await client.GetFromJsonAsync<PagedResponse<TrackResponse>>(
            "/api/catalog/tracks");

        Assert.NotNull(page);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
        Assert.Equal(
            ["Bohemian Rhapsody", "Don't Stop Me Now", "Sultans of Swing"],
            page.Items.Select(item => item.Title).OrderBy(title => title));

        var search = await client.GetFromJsonAsync<List<TrackResponse>>(
            $"/api/catalog/tracks/search?q={Uri.EscapeDataString("bohemain rhapsody")}");

        Assert.NotNull(search);
        Assert.Contains(search, item => item.Title == "Bohemian Rhapsody");
        Assert.DoesNotContain(search, item => item.Title == "Sultans of Swing");
    }

    [Fact]
    public async Task RepeatBatch_ReturnsDuplicates_AndDoesNotGrowTheLibrary()
    {
        using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory, Guid.NewGuid());

        await client.PostAsJsonAsync("/api/catalog/tracks/batch", DefaultRequest());
        var repeatResponse = await client.PostAsJsonAsync(
            "/api/catalog/tracks/batch", DefaultRequest());
        var repeated = await repeatResponse.Content.ReadFromJsonAsync<ImportTracksResponse>();

        Assert.Equal(HttpStatusCode.Accepted, repeatResponse.StatusCode);
        Assert.NotNull(repeated);
        Assert.Equal(3, repeated.Submitted);
        Assert.Equal(0, repeated.Imported);
        Assert.Equal(3, repeated.Duplicates);

        var page = await client.GetFromJsonAsync<PagedResponse<TrackResponse>>(
            "/api/catalog/tracks");
        Assert.NotNull(page);
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task TwoUsers_ImportingSameTracks_BothSeeTheirOwnLibrary()
    {
        using var factory = await CreateFactoryAsync();
        using var clientA = CreateClient(factory, Guid.NewGuid());
        using var clientB = CreateClient(factory, Guid.NewGuid());

        var responseA = await clientA.PostAsJsonAsync(
            "/api/catalog/tracks/batch", DefaultRequest());
        var responseB = await clientB.PostAsJsonAsync(
            "/api/catalog/tracks/batch", DefaultRequest());

        var importedA = await responseA.Content.ReadFromJsonAsync<ImportTracksResponse>();
        var importedB = await responseB.Content.ReadFromJsonAsync<ImportTracksResponse>();
        var pageA = await clientA.GetFromJsonAsync<PagedResponse<TrackResponse>>(
            "/api/catalog/tracks");
        var pageB = await clientB.GetFromJsonAsync<PagedResponse<TrackResponse>>(
            "/api/catalog/tracks");

        Assert.Equal(3, importedA!.Imported);
        Assert.Equal(3, importedB!.Imported);
        Assert.Equal(3, pageA!.TotalCount);
        Assert.Equal(3, pageB!.TotalCount);
    }

    [Fact]
    public async Task SearchAndList_NeverExposeAnotherUsersTracks()
    {
        using var factory = await CreateFactoryAsync();
        using var clientA = CreateClient(factory, Guid.NewGuid());
        using var clientB = CreateClient(factory, Guid.NewGuid());

        await clientA.PostAsJsonAsync(
            "/api/catalog/tracks/batch",
            CreateRequest(("a-1", "Bohemian Rhapsody", "Queen", 355)));
        await clientB.PostAsJsonAsync(
            "/api/catalog/tracks/batch",
            CreateRequest(("b-1", "Sultans of Swing", "Dire Straits", 348)));

        var searchAForSultans = await clientA.GetFromJsonAsync<List<TrackResponse>>(
            $"/api/catalog/tracks/search?q={Uri.EscapeDataString("sultans of swing")}");
        var searchBForBohemian = await clientB.GetFromJsonAsync<List<TrackResponse>>(
            $"/api/catalog/tracks/search?q={Uri.EscapeDataString("bohemian rhapsody")}");
        var pageA = await clientA.GetFromJsonAsync<PagedResponse<TrackResponse>>(
            "/api/catalog/tracks");
        var pageB = await clientB.GetFromJsonAsync<PagedResponse<TrackResponse>>(
            "/api/catalog/tracks");

        Assert.Empty(searchAForSultans!);
        Assert.Empty(searchBForBohemian!);
        Assert.Single(pageA!.Items);
        Assert.Single(pageB!.Items);
        Assert.Equal("Bohemian Rhapsody", pageA.Items[0].Title);
        Assert.Equal("Sultans of Swing", pageB.Items[0].Title);
    }

    [Fact]
    public async Task Import_InvalidatesCachedLibrary()
    {
        using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory, Guid.NewGuid());

        await client.PostAsJsonAsync(
            "/api/catalog/tracks/batch",
            CreateRequest(("local-1", "Bohemian Rhapsody", "Queen", 355)));

        var firstPage = await client.GetFromJsonAsync<PagedResponse<TrackResponse>>(
            "/api/catalog/tracks");
        Assert.Equal(1, firstPage!.TotalCount);

        await client.PostAsJsonAsync(
            "/api/catalog/tracks/batch",
            CreateRequest(("local-2", "Sultans of Swing", "Dire Straits", 348)));

        var secondPage = await client.GetFromJsonAsync<PagedResponse<TrackResponse>>(
            "/api/catalog/tracks");
        Assert.Equal(2, secondPage!.TotalCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task Batch_EmptyOrOversized_ReturnsBadRequest(int itemCount)
    {
        using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory, Guid.NewGuid());

        var items = Enumerable.Range(0, itemCount)
            .Select(index => ($"local-{index}", $"Title {index}", "Artist", 200))
            .ToArray();

        var response = await client.PostAsJsonAsync(
            "/api/catalog/tracks/batch", CreateRequest(items));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var page = await client.GetFromJsonAsync<PagedResponse<TrackResponse>>(
            "/api/catalog/tracks");
        Assert.Equal(0, page!.TotalCount);
    }

    [Fact]
    public async Task Batch_MissingTitle_ReturnsBadRequestAndWritesNothing()
    {
        using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory, Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            "/api/catalog/tracks/batch",
            CreateRequest(
                ("valid", "Bohemian Rhapsody", "Queen", 355),
                ("invalid", "  ", "Queen", 355)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var page = await client.GetFromJsonAsync<PagedResponse<TrackResponse>>(
            "/api/catalog/tracks");
        Assert.Equal(0, page!.TotalCount);
    }

    [Fact]
    public async Task Search_BlankQuery_ReturnsBadRequest()
    {
        using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory, Guid.NewGuid());

        var response = await client.GetAsync("/api/catalog/tracks/search?q=%20%20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Search_NoMatches_ReturnsEmptyList()
    {
        using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory, Guid.NewGuid());

        await client.PostAsJsonAsync("/api/catalog/tracks/batch", DefaultRequest());

        var results = await client.GetFromJsonAsync<List<TrackResponse>>(
            "/api/catalog/tracks/search?q=zzzzzz");

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task List_OutOfRangePaging_IsClamped()
    {
        using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory, Guid.NewGuid());

        await client.PostAsJsonAsync("/api/catalog/tracks/batch", DefaultRequest());

        var page = await client.GetFromJsonAsync<PagedResponse<TrackResponse>>(
            "/api/catalog/tracks?page=0&pageSize=9999");

        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(200, page.PageSize);
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task Lyrics_AreScopedToTheOwner_AndExposeSyncedVersion()
    {
        using var factory = await CreateFactoryAsync();
        var userIdA = Guid.NewGuid();
        var userIdB = Guid.NewGuid();
        using var clientA = CreateClient(factory, userIdA);
        using var clientB = CreateClient(factory, userIdB);

        await clientA.PostAsJsonAsync(
            "/api/catalog/tracks/batch",
            CreateRequest(("local-a", "Bohemian Rhapsody", "Queen", 355)));

        var options = new DbContextOptionsBuilder<Lyra.Catalog.Api.Data.CatalogDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        Guid trackId;

        await using (var db = new Lyra.Catalog.Api.Data.CatalogDbContext(options))
        {
            var track = await db.Tracks.SingleAsync(item => item.Title == "Bohemian Rhapsody");
            trackId = track.Id;
            var repository = new Lyra.Catalog.Api.Repositories.TrackRepository(db);
            await repository.ApplyLyricsAsync(
                trackId,
                "plain lyrics",
                "[00:01.00] synced lyrics",
                null,
                CancellationToken.None);
        }

        var lyrics = await clientA.GetFromJsonAsync<TrackLyricsResponse>(
            $"/api/catalog/tracks/{trackId}/lyrics");

        Assert.NotNull(lyrics);
        Assert.Equal(trackId, lyrics.TrackId);
        Assert.False(lyrics.IsEnriched);
        Assert.Equal("plain lyrics", lyrics.PlainLyrics);
        Assert.Equal("[00:01.00] synced lyrics", lyrics.SyncedLyrics);

        var page = await clientA.GetFromJsonAsync<PagedResponse<TrackResponse>>(
            "/api/catalog/tracks");
        var listed = Assert.Single(page!.Items);
        Assert.True(listed.HasLyrics);
        Assert.True(listed.HasSyncedLyrics);

        var forbidden = await clientB.GetAsync($"/api/catalog/tracks/{trackId}/lyrics");
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WithoutToken_ReturnUnauthorized()
    {
        using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);

        var listResponse = await client.GetAsync("/api/catalog/tracks");
        var searchResponse = await client.GetAsync("/api/catalog/tracks/search?q=queen");
        var batchResponse = await client.PostAsJsonAsync(
            "/api/catalog/tracks/batch", DefaultRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, searchResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, batchResponse.StatusCode);
    }
}
