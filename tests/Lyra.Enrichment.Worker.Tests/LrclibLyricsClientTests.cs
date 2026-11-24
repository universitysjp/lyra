namespace Lyra.Enrichment.Worker.Tests;

using System.Net;
using System.Text;
using Lyra.Enrichment.Worker.Configuration;
using Lyra.Enrichment.Worker.Services.Lyrics;
using Lyra.Enrichment.Worker.Tests.TestDoubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

public class LrclibLyricsClientTests
{
    [Fact]
    public async Task GetLyrics_EncodesQueryAndReturnsPlainLyrics()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse("""
                {
                  "plainLyrics": "line one\nline two",
                  "syncedLyrics": "[00:01.00] line one",
                  "instrumental": false
                }
                """)));
        var client = CreateClient(handler);

        var lyrics = await client.GetLyricsAsync(
            "A & B / C",
            "Sigur Rós",
            "Album & More",
            245,
            CancellationToken.None);

        Assert.Equal("line one\nline two", lyrics.PlainLyrics);
        Assert.Equal("[00:01.00] line one", lyrics.SyncedLyrics);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            "http://localhost/api/get" +
            "?track_name=A%20%26%20B%20%2F%20C" +
            "&artist_name=Sigur%20R%C3%B3s" +
            "&album_name=Album%20%26%20More" +
            "&duration=245",
            request.RequestUri?.AbsoluteUri);
        Assert.Equal(
            "Lyra.Enrichment.Worker.Tests/1.0",
            request.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task GetLyrics_OmitsUnsupportedDurationAndBlankAlbum()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse("""{"plainLyrics":"ok","instrumental":false}""")));
        var client = CreateClient(handler);

        await client.GetLyricsAsync(
            "Song",
            "Artist",
            "   ",
            4000,
            CancellationToken.None);

        var uri = Assert.Single(handler.Requests).RequestUri?.AbsoluteUri;
        Assert.DoesNotContain("duration=", uri);
        Assert.DoesNotContain("album_name=", uri);
    }

    [Fact]
    public async Task GetLyrics_NotFoundAndInstrumentalReturnNull()
    {
        var notFoundClient = CreateClient(new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        var instrumentalClient = CreateClient(new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse("""
                {"plainLyrics":null,"instrumental":true}
                """))));

        Assert.False((await notFoundClient.GetLyricsAsync(
            "Song", "Artist", null, 100, CancellationToken.None)).HasLyrics);
        Assert.False((await instrumentalClient.GetLyricsAsync(
            "Song", "Artist", null, 100, CancellationToken.None)).HasLyrics);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.BadGateway, true)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    public async Task GetLyrics_MapsHttpStatusForRetryDecisions(
        HttpStatusCode status,
        bool expectedTransient)
    {
        var client = CreateClient(new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(status))));

        var exception = await Assert.ThrowsAsync<LrclibRequestException>(
            () => client.GetLyricsAsync(
                "Song", "Artist", null, 100, CancellationToken.None));

        Assert.Equal((int)status, exception.StatusCode);
        Assert.Equal(expectedTransient, exception.IsTransient);
    }

    [Fact]
    public async Task GetLyrics_MalformedJsonIsPermanent()
    {
        var client = CreateClient(new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse("{not-json"))));

        var exception = await Assert.ThrowsAsync<LrclibRequestException>(
            () => client.GetLyricsAsync(
                "Song", "Artist", null, 100, CancellationToken.None));

        Assert.False(exception.IsTransient);
    }

    [Fact]
    public async Task GetLyrics_NetworkFailureIsTransient()
    {
        var client = CreateClient(new RecordingHttpMessageHandler((_, _) =>
            throw new HttpRequestException(
                "network unavailable",
                inner: null,
                HttpStatusCode.ServiceUnavailable)));

        var exception = await Assert.ThrowsAsync<LrclibRequestException>(
            () => client.GetLyricsAsync(
                "Song", "Artist", null, 100, CancellationToken.None));

        Assert.True(exception.IsTransient);
        Assert.Equal(503, exception.StatusCode);
    }

    [Fact]
    public async Task GetLyrics_CallerCancellationPropagates()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return JsonResponse("{}");
        }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetLyricsAsync(
                "Song", "Artist", null, 100, cancellation.Token));
    }

    [Fact]
    public void BuildRequestPath_RequiresTitleAndArtist()
    {
        Assert.Throws<ArgumentException>(
            () => LrclibLyricsClient.BuildRequestPath("", "Artist", 100));
        Assert.Throws<ArgumentException>(
            () => LrclibLyricsClient.BuildRequestPath("Song", " ", 100));
    }

    private static LrclibLyricsClient CreateClient(
        HttpMessageHandler handler,
        IMemoryCache? cache = null)
    {
        var options = new LrclibOptions
        {
            MinRequestIntervalMilliseconds = 0,
            UserAgent = "Lyra.Enrichment.Worker.Tests/1.0"
        };
        var throttle = new LrclibRequestThrottle(
            Options.Create(options),
            new FakeTimeProvider(DateTimeOffset.UtcNow));
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);

        return new LrclibLyricsClient(
            httpClient,
            throttle,
            Options.Create(options),
            cache);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}
