namespace Lyra.PlaylistRag.Api.Tests;

using Lyra.AI.Identity;
using Lyra.PlaylistRag.Api.Configuration;
using Lyra.PlaylistRag.Api.DTOs.Requests;
using Lyra.PlaylistRag.Api.Exceptions;
using Lyra.PlaylistRag.Api.Models.Playlists;
using Lyra.PlaylistRag.Api.Services.Playlists;
using Lyra.PlaylistRag.Api.Tests.TestDoubles;
using Microsoft.Extensions.Options;
using Xunit;

public class PlaylistGenerationServiceTests
{
    [Fact]
    public async Task GenerateAsync_MapsSelectedCandidatesToTheCallersLocalIds()
    {
        var userId = Guid.NewGuid();
        var search = new FakePlaylistCandidateSearch();
        var selector = new FakePlaylistSelector();
        var connection = new FakeUserAiConnectionClient();
        var service = CreateService(userId, connection, search, selector);

        var response = await service.GenerateAsync(
            userId,
            new GeneratePlaylistRequest("calm evening", 2),
            CancellationToken.None);

        Assert.Equal("Evening glow", response.Title);
        Assert.Equal(["local-1", "local-2"], response.Tracks.Select(track => track.LocalTrackId));
        Assert.Equal("First", response.Tracks[0].Title);
        Assert.Equal("Fits the request 1.", response.Tracks[0].Reason);
        Assert.Equal(userId, connection.LastUserId);
        Assert.Equal(2, selector.LastLimit);
        Assert.Equal(userId, search.LastUserId);
    }

    [Fact]
    public async Task GenerateAsync_EmptyFilterResultReturnsAnEmptyPlaylistAndSkipsSelection()
    {
        var userId = Guid.NewGuid();
        var search = new FakePlaylistCandidateSearch(
            (_, _, _) => new PlaylistCandidateSearchResult([], HasIndexedTracks: true));
        var selector = new FakePlaylistSelector();
        var service = CreateService(userId, new FakeUserAiConnectionClient(), search, selector);

        var response = await service.GenerateAsync(
            userId,
            new GeneratePlaylistRequest("nothing matches this", null),
            CancellationToken.None);

        Assert.Empty(response.Tracks);
        Assert.Equal("Evening glow", response.Title);
        Assert.Equal(0, selector.Calls);
        Assert.Equal(20, search.LastLimit);
    }

    [Fact]
    public async Task GenerateAsync_NoIndexedTracksIsAConflict()
    {
        var userId = Guid.NewGuid();
        var search = new FakePlaylistCandidateSearch(
            (_, _, _) => new PlaylistCandidateSearchResult([], HasIndexedTracks: false));
        var service = CreateService(
            userId,
            new FakeUserAiConnectionClient(),
            search,
            new FakePlaylistSelector());

        await Assert.ThrowsAsync<PlaylistLibraryNotReadyException>(
            () => service.GenerateAsync(
                userId,
                new GeneratePlaylistRequest("calm evening", null),
                CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_MissingAiConnectionPropagatesTheConflictReason()
    {
        var userId = Guid.NewGuid();
        var connections = new FakeUserAiConnectionClient(
            (id, _) => Task.FromException<Lyra.Contracts.AI.AiConnectionSecret>(
                new AiConnectionNotFoundException(id)));
        var service = CreateService(
            userId,
            connections,
            new FakePlaylistCandidateSearch(),
            new FakePlaylistSelector());

        await Assert.ThrowsAsync<AiConnectionNotFoundException>(
            () => service.GenerateAsync(
                userId,
                new GeneratePlaylistRequest("calm evening", null),
                CancellationToken.None));
    }

    [Theory]
    [InlineData("", 20)]
    [InlineData("   ", 20)]
    [InlineData("valid", 0)]
    [InlineData("valid", 51)]
    public async Task GenerateAsync_InvalidRequestIsRejectedBeforeCallingIdentity(string prompt, int limit)
    {
        var userId = Guid.NewGuid();
        var connections = new FakeUserAiConnectionClient();
        var service = CreateService(
            userId,
            connections,
            new FakePlaylistCandidateSearch(),
            new FakePlaylistSelector());

        await Assert.ThrowsAsync<PlaylistValidationException>(
            () => service.GenerateAsync(
                userId,
                new GeneratePlaylistRequest(prompt, limit),
                CancellationToken.None));

        Assert.Equal(0, connections.Calls);
    }

    private static PlaylistGenerationService CreateService(
        Guid userId,
        FakeUserAiConnectionClient connections,
        FakePlaylistCandidateSearch search,
        FakePlaylistSelector selector) =>
        new(
            connections,
            new FakePlaylistQueryPlanner(),
            search,
            selector,
            new FakeTrackEmbeddingGenerator(),
            Options.Create(new PlaylistOptions()));
}
