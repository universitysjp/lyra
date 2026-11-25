namespace Lyra.PlaylistRag.Api.Tests;

using System.Text.Json;
using Lyra.Contracts.AI;
using Lyra.PlaylistRag.Api.Configuration;
using Lyra.PlaylistRag.Api.Exceptions;
using Lyra.PlaylistRag.Api.Models.Playlists;
using Lyra.PlaylistRag.Api.Services.Playlists;
using Lyra.PlaylistRag.Api.Tests.TestDoubles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Xunit;

public class PlaylistSelectorTests
{
    private static readonly AiConnectionSecret Connection = new(
        AiProvider.OpenAI,
        "gpt-test",
        "sk-test",
        null);

    private static readonly PlaylistQueryPlan Plan = new(
        "calm evening",
        ["calm"],
        ["evening"],
        2,
        6,
        null,
        null,
        "Evening",
        "Soft songs for the evening.");

    [Fact]
    public async Task SelectAsync_ReturnsValidatedIndicesInModelOrder()
    {
        var fake = CreateFake("""
            {
              "tracks": [
                { "index": 2, "reason": "Matches the quiet mood." },
                { "index": 1, "reason": "A good opener." }
              ]
            }
            """);
        var selector = CreateSelector(fake);

        var selection = await selector.SelectAsync(
            "calm evening",
            Plan,
            Candidates(3),
            2,
            Connection,
            "session-1",
            CancellationToken.None);

        Assert.Equal([2, 1], selection.Tracks.Select(item => item.Index));
        Assert.Equal("Matches the quiet mood.", selection.Tracks[0].Reason);
        Assert.NotNull(fake.Options?.ResponseFormat);
    }

    [Theory]
    [InlineData("""{"tracks":[]}""")]
    [InlineData("""{"tracks":[{"index":0,"reason":"x"}]}""")]
    [InlineData("""{"tracks":[{"index":4,"reason":"x"}]}""")]
    [InlineData("""{"tracks":[{"index":1,"reason":"x"},{"index":1,"reason":"y"}]}""")]
    [InlineData("""{"tracks":[{"index":1,"reason":"x"},{"index":2,"reason":"y"},{"index":3,"reason":"z"}]}""")]
    [InlineData("""{"tracks":[{"index":1,"reason":"  "}]}""")]
    [InlineData("""{"tracks":null}""")]
    public async Task SelectAsync_InvalidSelectionIsRejected(string json)
    {
        var selector = CreateSelector(CreateFake(json));

        await Assert.ThrowsAsync<PlaylistGenerationException>(
            () => selector.SelectAsync(
                "calm evening",
                Plan,
                Candidates(3),
                2,
                Connection,
                "session-1",
                CancellationToken.None));
    }

    [Fact]
    public async Task SelectAsync_RetriesOnceWhenFirstStructuredResponseIsEmpty()
    {
        var calls = 0;
        var fake = new FakeChatClient((_, _, _) =>
        {
            calls++;
            var text = calls == 1
                ? string.Empty
                : """{"tracks":[{"index":1,"reason":"Fits the request."}]}""";

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
            {
                FinishReason = calls == 1 ? ChatFinishReason.Length : ChatFinishReason.Stop
            });
        });
        var selector = CreateSelector(fake);

        var selection = await selector.SelectAsync(
            "calm evening",
            Plan,
            Candidates(3),
            2,
            Connection,
            "session-1",
            CancellationToken.None);

        Assert.Equal(2, calls);
        Assert.Equal(1, selection.Tracks.Single().Index);
    }

    [Fact]
    public async Task SelectAsync_EmptyResponseReportsProviderFinishReason()
    {
        var fake = new FakeChatClient((_, _, _) => Task.FromResult(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty))
            {
                FinishReason = ChatFinishReason.ContentFilter
            }));
        var selector = CreateSelector(fake);

        var exception = await Assert.ThrowsAsync<PlaylistGenerationException>(
            () => selector.SelectAsync(
                "calm evening",
                Plan,
                Candidates(3),
                2,
                Connection,
                "session-1",
                CancellationToken.None));

        Assert.Contains("finish reason: content_filter", exception.Message);
    }

    [Fact]
    public async Task SelectAsync_MalformedJsonIsRejected()
    {
        var selector = CreateSelector(CreateFake("not-json"));

        await Assert.ThrowsAsync<PlaylistGenerationException>(
            () => selector.SelectAsync(
                "calm evening",
                Plan,
                Candidates(3),
                2,
                Connection,
                "session-1",
                CancellationToken.None));
    }

    [Fact]
    public async Task SelectAsync_SendsIndicesWithoutServerIdentifiers()
    {
        var fake = CreateFake("""{"tracks":[{"index":1,"reason":"Fits."}]}""");
        var selector = CreateSelector(fake);

        await selector.SelectAsync(
            "calm evening",
            Plan,
            Candidates(3),
            2,
            Connection,
            "session-1",
            CancellationToken.None);

        var prompt = fake.Messages!.Single(message => message.Role == ChatRole.User).Text;
        Assert.Contains("\"index\":1", prompt);
        Assert.DoesNotContain("local-", prompt);
        Assert.DoesNotContain("00000000-", prompt);
    }

    private static PlaylistSelector CreateSelector(FakeChatClient fake) =>
        new(
            new FakeAiChatClientFactory(fake),
            Options.Create(new PlaylistOptions()));

    private static FakeChatClient CreateFake(string json) =>
        new((_, _, _) => Task.FromResult(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, json))));

    private static PlaylistCandidate[] Candidates(int count) =>
        Enumerable.Range(1, count)
            .Select(index => new PlaylistCandidate(
                index,
                Guid.NewGuid(),
                $"local-{index}",
                $"Track {index}",
                $"Artist {index}",
                "Album",
                180,
                "calm",
                ["evening"],
                3,
                "A calm evening track.",
                0.1 * index))
            .ToArray();
}
