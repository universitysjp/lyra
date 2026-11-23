namespace Lyra.Enrichment.Worker.Tests;

using System.Text.Json;
using Lyra.Contracts.AI;
using Lyra.Enrichment.Worker.Configuration;
using Lyra.Enrichment.Worker.Models;
using Lyra.Enrichment.Worker.Services.Enrichment;
using Lyra.Enrichment.Worker.Tests.TestDoubles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Xunit;

public class TrackAnalysisServiceTests
{
    private static readonly AiConnectionSecret Connection = new(
        AiProvider.OpenAI,
        "gpt-test",
        "sk-test",
        null);

    [Fact]
    public async Task AnalyzeAsync_ParsesAndNormalizesStructuredOutput()
    {
        var fake = CreateFake("""
            {
              "primaryMood": "  melancholic  ",
              "themes": ["night", " dreamy ", "NIGHT", "", "loss"],
              "energyLevel": 3
            }
            """);
        var service = CreateService(fake);

        var analysis = await service.AnalyzeAsync(
            Request("Song", "Artist", null, null),
            Connection,
            "session-1",
            CancellationToken.None);

        Assert.Equal("melancholic", analysis.PrimaryMood);
        Assert.Equal(["night", "dreamy", "loss"], analysis.Themes);
        Assert.Equal(3, analysis.EnergyLevel);
        Assert.True(fake.IsDisposed);
    }

    [Fact]
    public async Task AnalyzeAsync_SendsBoundedPromptAndTreatsMissingLyricsAsMetadataOnly()
    {
        var fake = CreateFake(ValidJson());
        var service = CreateService(fake, maxLyricsCharacters: 5);

        await service.AnalyzeAsync(
            Request("Title", "Artist", "Album", null),
            Connection,
            "session-1",
            CancellationToken.None);

        var prompt = fake.Messages!.Single(message => message.Role == ChatRole.User).Text;
        Assert.DoesNotContain("\"lyrics\"", prompt);
        Assert.Contains("\"title\":\"Title\"", prompt);
        Assert.NotNull(fake.Options?.ResponseFormat);
    }

    [Fact]
    public async Task AnalyzeAsync_BoundsLyricsSentToTheModel()
    {
        var fake = CreateFake(ValidJson());
        var service = CreateService(fake, maxLyricsCharacters: 5);

        await service.AnalyzeAsync(
            Request("Title", "Artist", null, "123456789"),
            Connection,
            "session-1",
            CancellationToken.None);

        var prompt = fake.Messages!.Single(message => message.Role == ChatRole.User).Text;
        Assert.Contains("12345", prompt);
        Assert.DoesNotContain("6789", prompt);
    }

    [Theory]
    [InlineData("""{"primaryMood":"","themes":["x"],"energyLevel":5}""")]
    [InlineData("""{"primaryMood":"mood","themes":null,"energyLevel":5}""")]
    [InlineData("""{"primaryMood":"mood","themes":["x"],"energyLevel":0}""")]
    [InlineData("""{"primaryMood":"mood","themes":["x"],"energyLevel":11}""")]
    public async Task AnalyzeAsync_InvalidDomainValuesAreRejected(string json)
    {
        var service = CreateService(CreateFake(json));

        await Assert.ThrowsAsync<EnrichmentOutputException>(
            () => service.AnalyzeAsync(
                Request("Title", "Artist", null, "lyrics"),
                Connection,
                "session-1",
                CancellationToken.None));
    }

    [Fact]
    public async Task AnalyzeAsync_OverlongPrimaryMoodIsRejected()
    {
        var service = CreateService(CreateFake(JsonSerializer.Serialize(new
        {
            primaryMood = new string('x', 129),
            themes = new[] { "ambient" },
            energyLevel = 2
        })));

        await Assert.ThrowsAsync<EnrichmentOutputException>(
            () => service.AnalyzeAsync(
                Request("Title", "Artist", null, "lyrics"),
                Connection,
                "session-1",
                CancellationToken.None));
    }

    [Fact]
    public async Task AnalyzeAsync_MalformedJsonIsRejected()
    {
        var service = CreateService(CreateFake("not-json"));

        await Assert.ThrowsAsync<EnrichmentOutputException>(
            () => service.AnalyzeAsync(
                Request("Title", "Artist", null, "lyrics"),
                Connection,
                "session-1",
                CancellationToken.None));
    }

    private static TrackAnalysisService CreateService(
        FakeChatClient fake,
        int maxLyricsCharacters = 12_000) =>
        new(
            new FakeAiChatClientFactory(fake),
            Options.Create(new EnrichmentOptions
            {
                MaxLyricsCharacters = maxLyricsCharacters
            }));

    private static FakeChatClient CreateFake(string responseJson) =>
        new((_, _, _) => Task.FromResult(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, responseJson))));

    private static TrackAnalysisRequest Request(
        string title,
        string artist,
        string? album,
        string? lyrics) =>
        new(title, artist, album, 180, lyrics);

    private static string ValidJson() =>
        JsonSerializer.Serialize(new
        {
            primaryMood = "calm",
            themes = new[] { "ambient" },
            energyLevel = 2
        });
}
