namespace Lyra.PlaylistRag.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using Lyra.AI.Identity;
using Lyra.PlaylistRag.Api.DTOs.Requests;
using Lyra.PlaylistRag.Api.DTOs.Responses;
using Lyra.PlaylistRag.Api.Exceptions;
using Lyra.PlaylistRag.Api.Models;
using Lyra.PlaylistRag.Api.Models.Playlists;
using Lyra.PlaylistRag.Api.Services.Embeddings;
using Lyra.PlaylistRag.Api.Services.Playlists;
using Lyra.PlaylistRag.Api.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pgvector;
using Xunit;

public class PlaylistEndpointsTests : IClassFixture<VectorPostgresTestFixture>
{
    private readonly VectorPostgresTestFixture _fixture;

    public PlaylistEndpointsTests(VectorPostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Generate_ReturnsTheCallersOwnLocalTracks()
    {
        await VectorTestDatabase.ResetAsync(_fixture.ConnectionString);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await SeedTrackAsync(userId, "local-1", "First", "Artist A");
        await SeedTrackAsync(userId, "local-2", "Second", "Artist B");
        await SeedTrackAsync(otherUserId, "local-other", "Foreign", "Artist C");

        using var factory = CreateFactory();
        using var client = CreateClient(factory, userId);

        var response = await client.PostAsJsonAsync(
            "/api/playlists/generate",
            new GeneratePlaylistRequest("calm evening", 2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var playlist = await response.Content.ReadFromJsonAsync<GeneratePlaylistResponse>();

        Assert.NotNull(playlist);
        Assert.Equal("Evening glow", playlist.Title);
        Assert.Equal(2, playlist.Tracks.Count);
        Assert.All(playlist.Tracks, track => Assert.StartsWith("local-", track.LocalTrackId));
        Assert.DoesNotContain(playlist.Tracks, track => track.LocalTrackId == "local-other");
        Assert.All(playlist.Tracks, track => Assert.False(string.IsNullOrWhiteSpace(track.Reason)));
    }

    [Fact]
    public async Task Generate_AnonymousRequestIsRejected()
    {
        await VectorTestDatabase.ResetAsync(_fixture.ConnectionString);
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/playlists/generate",
            new GeneratePlaylistRequest("calm evening", 10));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("   ", 10)]
    [InlineData("calm evening", 0)]
    [InlineData("calm evening", 51)]
    public async Task Generate_InvalidRequestReturnsBadRequest(string prompt, int limit)
    {
        await VectorTestDatabase.ResetAsync(_fixture.ConnectionString);
        using var factory = CreateFactory();
        using var client = CreateClient(factory, Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            "/api/playlists/generate",
            new GeneratePlaylistRequest(prompt, limit));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_MissingAiConnectionReturnsConflict()
    {
        await VectorTestDatabase.ResetAsync(_fixture.ConnectionString);
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IUserAiConnectionClient>();
            services.AddSingleton<IUserAiConnectionClient>(
                new FakeUserAiConnectionClient(
                    (userId, _) => Task.FromException<Lyra.Contracts.AI.AiConnectionSecret>(
                        new AiConnectionNotFoundException(userId))));
        });
        using var client = CreateClient(factory, Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            "/api/playlists/generate",
            new GeneratePlaylistRequest("calm evening", 10));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Generate_NoIndexedTracksReturnsConflict()
    {
        await VectorTestDatabase.ResetAsync(_fixture.ConnectionString);
        using var factory = CreateFactory();
        using var client = CreateClient(factory, Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            "/api/playlists/generate",
            new GeneratePlaylistRequest("calm evening", 10));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Generate_FilteredEmptyResultReturnsAnEmptyPlaylist()
    {
        await VectorTestDatabase.ResetAsync(_fixture.ConnectionString);
        var userId = Guid.NewGuid();
        await SeedTrackAsync(userId, "local-1", "First", "Artist A");

        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IPlaylistQueryPlanner>();
            services.AddScoped<IPlaylistQueryPlanner>(_ =>
                new FakePlaylistQueryPlanner(() => new PlaylistQueryPlan(
                    "nothing",
                    ["not-a-mood"],
                    [],
                    null,
                    null,
                    null,
                    null,
                    "Empty",
                    "No tracks match.")));
        });
        using var client = CreateClient(factory, userId);

        var response = await client.PostAsJsonAsync(
            "/api/playlists/generate",
            new GeneratePlaylistRequest("something impossible", 10));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var playlist = await response.Content.ReadFromJsonAsync<GeneratePlaylistResponse>();
        Assert.NotNull(playlist);
        Assert.Empty(playlist.Tracks);
        Assert.Equal("Empty", playlist.Title);
    }

    [Fact]
    public async Task Generate_InvalidSelectionReturnsBadGateway()
    {
        await VectorTestDatabase.ResetAsync(_fixture.ConnectionString);
        var userId = Guid.NewGuid();
        await SeedTrackAsync(userId, "local-1", "First", "Artist A");

        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IPlaylistSelector>();
            services.AddScoped<IPlaylistSelector>(_ =>
                new FakePlaylistSelector(_ => throw new PlaylistGenerationException(
                    "The AI provider returned an invalid playlist selection.")));
        });
        using var client = CreateClient(factory, userId);

        var response = await client.PostAsJsonAsync(
            "/api/playlists/generate",
            new GeneratePlaylistRequest("calm evening", 10));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private PlaylistApiFactory CreateFactory(Action<IServiceCollection>? configure = null) =>
        new(
            _fixture.ConnectionString,
            services =>
            {
                services.RemoveAll<IUserAiConnectionClient>();
                services.AddSingleton<IUserAiConnectionClient>(new FakeUserAiConnectionClient());
                services.RemoveAll<IPlaylistQueryPlanner>();
                services.AddScoped<IPlaylistQueryPlanner>(_ => new FakePlaylistQueryPlanner());
                services.RemoveAll<IPlaylistSelector>();
                services.AddScoped<IPlaylistSelector>(_ => new FakePlaylistSelector());
                services.RemoveAll<ITrackEmbeddingGenerator>();
                services.AddSingleton<ITrackEmbeddingGenerator>(new FakeTrackEmbeddingGenerator());

                configure?.Invoke(services);
            });

    private static HttpClient CreateClient(PlaylistApiFactory factory, Guid? userId)
    {
        var client = factory.CreateClient();

        if (userId is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.Value.ToString());
        }

        return client;
    }

    private async Task SeedTrackAsync(
        Guid userId,
        string localTrackId,
        string title,
        string artist)
    {
        await using var db = new Lyra.PlaylistRag.Api.Data.VectorDbContext(
            VectorTestDatabase.Options(_fixture.ConnectionString));
        var trackId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        db.UserTracks.Add(new UserTrack
        {
            UserId = userId,
            TrackId = trackId,
            LocalTrackId = localTrackId,
            Title = title,
            Artist = artist,
            Album = "Album",
            DurationSeconds = 180,
            ImportedAt = now
        });
        db.TrackEmbeddings.Add(new TrackEmbedding
        {
            TrackId = trackId,
            Title = title,
            Artist = artist,
            Album = "Album",
            DurationSeconds = 180,
            PrimaryMood = "calm",
            Themes = ["evening"],
            EnergyLevel = 3,
            SemanticSummary = "A calm evening track.",
            Embedding = VectorOf(),
            EmbeddingModel = new FakeTrackEmbeddingGenerator().Model,
            EmbeddingDimensions = TrackEmbedding.Dimensions,
            EnrichedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }

    private static Vector VectorOf()
    {
        var values = new float[TrackEmbedding.Dimensions];
        values[0] = 1;
        return new Vector(values);
    }
}
