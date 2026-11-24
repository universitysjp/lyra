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

public class LrclibFuzzyMatchingTests
{
    private const string TargetTitle = "As long as you love me.. (mp3.pm)";
    private const string TargetArtist = "Back Street Boys";
    private const int TargetDuration = 212;

    [Fact]
    public async Task ExactMatch_ReturnsLyricsAndResolvedMetadataWithoutSearch()
    {
        var handler = new RecordingHttpMessageHandler((request, _) =>
            Task.FromResult(request.RequestUri!.AbsolutePath == "/api/get"
                ? JsonResponse("""
                    {
                      "id": 99,
                      "trackName": "As Long as You Love Me",
                      "artistName": "Backstreet Boys",
                      "albumName": "Backstreet's Back",
                      "duration": 212,
                      "instrumental": false,
                      "plainLyrics": "exact lyrics",
                      "syncedLyrics": "[00:01.00] exact lyrics"
                    }
                    """)
                : JsonResponse("[]")));
        var client = CreateClient(handler);

        var lyrics = await client.GetLyricsAsync(
            TargetTitle,
            TargetArtist,
            "Backstreet's Back",
            TargetDuration,
            CancellationToken.None);

        Assert.Equal("exact lyrics", lyrics.PlainLyrics);
        Assert.Equal("[00:01.00] exact lyrics", lyrics.SyncedLyrics);
        Assert.Equal("As Long as You Love Me", lyrics.Metadata?.Title);
        Assert.Equal("Backstreet Boys", lyrics.Metadata?.Artist);
        Assert.Equal("Backstreet's Back", lyrics.Metadata?.Album);
        Assert.Equal(212, lyrics.Metadata?.DurationSeconds);
        Assert.Single(handler.Requests);
        Assert.DoesNotContain("mp3.pm", handler.Requests[0].RequestUri?.Query);
        Assert.Contains("album_name=Backstreet%27s%20Back", handler.Requests[0].RequestUri?.Query);
    }

    [Fact]
    public async Task ExactWithoutLyrics_FallsBackToV2Search()
    {
        var handler = new RecordingHttpMessageHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;

            return Task.FromResult(path switch
            {
                "/api/get" => JsonResponse(
                    """{"id":1,"trackName":"As Long as You Love Me","instrumental":false,"plainLyrics":null}"""),
                "/api/v2/search" => JsonResponse($"[{CandidateJson(
                    id: 42,
                    title: "As Long as You Love Me",
                    artist: "Backstreet Boys",
                    duration: 213)}]"),
                "/api/get/42" => JsonResponse(
                    """{"plainLyrics":"fallback lyrics","syncedLyrics":"[00:02.00] fallback","instrumental":false}"""),
                _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            });
        });
        var client = CreateClient(handler);

        var lyrics = await client.GetLyricsAsync(
            TargetTitle,
            TargetArtist,
            null,
            TargetDuration,
            CancellationToken.None);

        Assert.Equal("fallback lyrics", lyrics.PlainLyrics);
        Assert.Equal("[00:02.00] fallback", lyrics.SyncedLyrics);
        Assert.Equal("As Long as You Love Me", lyrics.Metadata?.Title);
        Assert.Equal("Backstreet Boys", lyrics.Metadata?.Artist);
        Assert.Equal(213, lyrics.Metadata?.DurationSeconds);
        Assert.Contains(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/v2/search");
    }

    [Fact]
    public async Task IncorrectLocalArtist_StrongTitleAndDurationIsAccepted()
    {
        var handler = FallbackHandler(CandidateJson(
            id: 7,
            title: "As Long as You Love Me",
            artist: "Real Backstreet Boys",
            duration: TargetDuration));
        var client = CreateClient(handler);

        var lyrics = await client.GetLyricsAsync(
            TargetTitle,
            "Downloaded from some music site",
            null,
            TargetDuration,
            CancellationToken.None);

        Assert.Equal("fallback lyrics", lyrics.PlainLyrics);
        Assert.Contains(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/7");
    }

    [Fact]
    public async Task MatchingArtistButWrongTitle_DoesNotBeatStrongTitleMatch()
    {
        var handler = FallbackHandler(
            CandidateJson(10, "Completely Different Song", TargetArtist, TargetDuration),
            CandidateJson(11, "As Long as You Love Me", "Wrong Artist", TargetDuration));
        var client = CreateClient(handler);

        await client.GetLyricsAsync(
            TargetTitle,
            TargetArtist,
            null,
            TargetDuration,
            CancellationToken.None);

        Assert.Contains(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/11");
        Assert.DoesNotContain(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/10");
    }

    [Fact]
    public async Task SimilarTitles_DifferentDurations_PrefersCloserDuration()
    {
        var handler = FallbackHandler(
            CandidateJson(20, "As Long as You Love Me", TargetArtist, 214),
            CandidateJson(21, "As Long as You Love Me", TargetArtist, 218));
        var client = CreateClient(handler);

        await client.GetLyricsAsync(
            TargetTitle,
            TargetArtist,
            null,
            TargetDuration,
            CancellationToken.None);

        Assert.Contains(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/20");
        Assert.DoesNotContain(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/21");
    }

    [Fact]
    public async Task HighestScoringCandidate_IsSelectedInsteadOfFirstApiResult()
    {
        var handler = FallbackHandler(
            CandidateJson(30, "Unrelated Local Download", TargetArtist, 300),
            CandidateJson(31, "As Long as You Love Me", TargetArtist, TargetDuration));
        var client = CreateClient(handler);

        await client.GetLyricsAsync(
            TargetTitle,
            TargetArtist,
            null,
            TargetDuration,
            CancellationToken.None);

        Assert.Contains(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/31");
        Assert.DoesNotContain(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/30");
    }

    [Fact]
    public async Task BelowThresholdCandidate_IsRejectedWithoutDetailFetch()
    {
        var handler = FallbackHandler(CandidateJson(
            id: 8,
            title: "Completely Different Song",
            artist: "Another Artist",
            duration: 400));
        var client = CreateClient(handler);

        var lyrics = await client.GetLyricsAsync(
            TargetTitle,
            TargetArtist,
            null,
            TargetDuration,
            CancellationToken.None);

        Assert.False(lyrics.HasLyrics);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath.StartsWith("/api/get/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HighestScoringWithoutLyrics_NextEligibleCandidateIsSelected()
    {
        var handler = FallbackHandler(
            CandidateJson(
                40,
                "As Long as You Love Me",
                TargetArtist,
                TargetDuration,
                hasPlainLyrics: false,
                hasSyncedLyrics: false,
                hasLyricsfile: false),
            CandidateJson(41, "As Long as You Love Me (Live)", TargetArtist, 214));
        var client = CreateClient(handler);

        var lyrics = await client.GetLyricsAsync(
            TargetTitle,
            TargetArtist,
            null,
            TargetDuration,
            CancellationToken.None);

        Assert.Equal("fallback lyrics", lyrics.PlainLyrics);
        Assert.Contains(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/41");
        Assert.DoesNotContain(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/40");
    }

    [Fact]
    public async Task HighestEligibleWithoutUsableLyrics_NextEligibleIsFetched()
    {
        var handler = FallbackHandler(
            id => id == 101
                ? """{"plainLyrics":"second candidate lyrics","instrumental":false}"""
                : """{"plainLyrics":null,"syncedLyrics":null,"instrumental":false}""",
            CandidateJson(100, "As Long as You Love Me", TargetArtist, TargetDuration),
            CandidateJson(101, "As Long as You Love Me (Live)", TargetArtist, 214));
        var client = CreateClient(handler);

        var lyrics = await client.GetLyricsAsync(
            TargetTitle,
            TargetArtist,
            null,
            TargetDuration,
            CancellationToken.None);

        Assert.Equal("second candidate lyrics", lyrics.PlainLyrics);
        Assert.Contains(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/100");
        Assert.Contains(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/101");
    }

    [Fact]
    public async Task CloserDuration_OutranksSlightlyBetterTitle()
    {
        // Exact title but 5s off versus a one-character title typo with an exact duration.
        // Higher duration weight must prefer the version whose synced lyrics align.
        var handler = FallbackHandler(
            CandidateJson(110, "As Long as You Love Me", TargetArtist, 217),
            CandidateJson(111, "As Long as You Love Mee", TargetArtist, TargetDuration));
        var client = CreateClient(handler);

        await client.GetLyricsAsync(
            TargetTitle,
            TargetArtist,
            null,
            TargetDuration,
            CancellationToken.None);

        Assert.Contains(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/111");
        Assert.DoesNotContain(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/110");
    }

    [Fact]
    public async Task HighestScoringWithLyrics_IsSelectedWithoutAlternativeFetches()
    {
        var handler = FallbackHandler(
            CandidateJson(50, "As Long as You Love Me", TargetArtist, TargetDuration),
            CandidateJson(51, "As Long as You Love Me (Live)", TargetArtist, 214));
        var client = CreateClient(handler);

        await client.GetLyricsAsync(
            TargetTitle,
            TargetArtist,
            null,
            TargetDuration,
            CancellationToken.None);

        Assert.Contains(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/50");
        Assert.DoesNotContain(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/51");
    }

    [Fact]
    public async Task SimilarScores_HighestStillWins()
    {
        var handler = FallbackHandler(
            CandidateJson(60, "As Long as You Love Me", TargetArtist, 212),
            CandidateJson(61, "As Long as You Love Me", TargetArtist, 213));
        var client = CreateClient(handler);

        await client.GetLyricsAsync(
            TargetTitle,
            TargetArtist,
            null,
            TargetDuration,
            CancellationToken.None);

        Assert.Contains(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/60");
        Assert.DoesNotContain(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get/61");
    }

    [Fact]
    public async Task InstrumentalCandidates_AreSkipped()
    {
        var handler = FallbackHandler(CandidateJson(
            id: 70,
            title: "As Long as You Love Me",
            artist: TargetArtist,
            duration: TargetDuration,
            instrumental: true));
        var client = CreateClient(handler);

        var lyrics = await client.GetLyricsAsync(
            TargetTitle,
            TargetArtist,
            null,
            TargetDuration,
            CancellationToken.None);

        Assert.False(lyrics.HasLyrics);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath.StartsWith("/api/get/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoCandidates_ReturnsEmpty()
    {
        var handler = new RecordingHttpMessageHandler((request, _) =>
            Task.FromResult(request.RequestUri!.AbsolutePath switch
            {
                "/api/get" => new HttpResponseMessage(HttpStatusCode.NotFound),
                "/api/v2/search" => JsonResponse("[]"),
                _ => throw new InvalidOperationException("Unexpected request")
            }));
        var client = CreateClient(handler);

        var lyrics = await client.GetLyricsAsync(
            TargetTitle,
            TargetArtist,
            null,
            TargetDuration,
            CancellationToken.None);

        Assert.False(lyrics.HasLyrics);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task MissingArtist_UsesTitleOnlySearch()
    {
        var handler = new RecordingHttpMessageHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;

            return Task.FromResult(path switch
            {
                "/api/v2/search" => JsonResponse($"[{CandidateJson(
                    id: 90,
                    title: "As Long as You Love Me",
                    artist: "Backstreet Boys",
                    duration: TargetDuration)}]"),
                "/api/get/90" => JsonResponse(
                    """{"plainLyrics":"title only lyrics","instrumental":false}"""),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            });
        });
        var client = CreateClient(handler);

        var lyrics = await client.GetLyricsAsync(
            TargetTitle,
            "   ",
            null,
            TargetDuration,
            CancellationToken.None);

        Assert.Equal("title only lyrics", lyrics.PlainLyrics);
        Assert.Contains(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/v2/search"
                && request.RequestUri.Query.Contains("track_name=", StringComparison.Ordinal)
                && !request.RequestUri.Query.Contains("artist_name=", StringComparison.Ordinal));
        Assert.DoesNotContain(
            handler.Requests,
            request => request.RequestUri!.AbsolutePath == "/api/get");
    }

    [Fact]
    public async Task RateLimit_ExposesRetryAfter()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(3)) }
            }));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<LrclibRequestException>(
            () => client.GetLyricsAsync(
                TargetTitle,
                TargetArtist,
                null,
                TargetDuration,
                CancellationToken.None));

        Assert.True(exception.IsTransient);
        Assert.Equal(429, exception.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(3), exception.RetryAfter);
    }

    [Fact]
    public async Task SuccessfulLookup_IsCached()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(
                """{"plainLyrics":"cached lyrics","syncedLyrics":"[00:01.00] cached","instrumental":false}""")));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = CreateClient(handler, cache);

        var first = await client.GetLyricsAsync(
            TargetTitle, TargetArtist, null, TargetDuration, CancellationToken.None);
        var second = await client.GetLyricsAsync(
            TargetTitle, TargetArtist, null, TargetDuration, CancellationToken.None);

        Assert.Equal("cached lyrics", first.PlainLyrics);
        Assert.Equal("[00:01.00] cached", second.SyncedLyrics);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task NotFoundLookup_IsCached()
    {
        var handler = new RecordingHttpMessageHandler((request, _) =>
            Task.FromResult(request.RequestUri!.AbsolutePath switch
            {
                "/api/get" => new HttpResponseMessage(HttpStatusCode.NotFound),
                "/api/v2/search" => JsonResponse("[]"),
                _ => throw new InvalidOperationException("Unexpected request")
            }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = CreateClient(handler, cache);

        Assert.False((await client.GetLyricsAsync(
            TargetTitle, TargetArtist, null, TargetDuration, CancellationToken.None)).HasLyrics);
        Assert.False((await client.GetLyricsAsync(
            TargetTitle, TargetArtist, null, TargetDuration, CancellationToken.None)).HasLyrics);

        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task AlbumChangesTheCacheKey()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(
                """{"plainLyrics":"album lyrics","instrumental":false}""")));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = CreateClient(handler, cache);

        await client.GetLyricsAsync(TargetTitle, TargetArtist, "Album One", TargetDuration, CancellationToken.None);
        await client.GetLyricsAsync(TargetTitle, TargetArtist, "Album One", TargetDuration, CancellationToken.None);
        await client.GetLyricsAsync(TargetTitle, TargetArtist, "Album Two", TargetDuration, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("album_name=Album%20One", handler.Requests[0].RequestUri?.Query);
        Assert.Contains("album_name=Album%20Two", handler.Requests[1].RequestUri?.Query);
    }

    [Fact]
    public async Task MaxCandidateDetailFetches_IsRespected()
    {
        var candidates = Enumerable.Range(0, 5)
            .Select(index => CandidateJson(
                80 + index,
                "As Long as You Love Me",
                TargetArtist,
                TargetDuration + index))
            .ToArray();
        var handler = FallbackHandler(
            _ => """{"plainLyrics":null,"syncedLyrics":null,"instrumental":false}""",
            candidates);
        var client = CreateClient(handler, configure: options => options.MaxCandidateDetailFetches = 2);

        var lyrics = await client.GetLyricsAsync(
            TargetTitle,
            TargetArtist,
            null,
            TargetDuration,
            CancellationToken.None);

        Assert.False(lyrics.HasLyrics);
        Assert.Equal(
            2,
            handler.Requests.Count(request =>
                request.RequestUri!.AbsolutePath.StartsWith("/api/get/", StringComparison.Ordinal)));
    }

    private static RecordingHttpMessageHandler FallbackHandler(
        params string[] candidates) =>
        FallbackHandler(
            _ => """{"plainLyrics":"fallback lyrics","instrumental":false}""",
            candidates);

    private static RecordingHttpMessageHandler FallbackHandler(
        Func<long, string> detailJson,
        params string[] candidates)
    {
        var candidatesJson = $"[{string.Join(',', candidates)}]";

        return new((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;

            return Task.FromResult(path switch
            {
                "/api/get" => new HttpResponseMessage(HttpStatusCode.NotFound),
                "/api/v2/search" => JsonResponse(candidatesJson),
                _ when path.StartsWith("/api/get/", StringComparison.Ordinal) =>
                    JsonResponse(detailJson(long.Parse(path["/api/get/".Length..]))),
                _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            });
        });
    }

    private static LrclibLyricsClient CreateClient(
        HttpMessageHandler handler,
        IMemoryCache? cache = null,
        Action<LrclibOptions>? configure = null)
    {
        var options = new LrclibOptions
        {
            MinRequestIntervalMilliseconds = 0,
            UserAgent = "Lyra.Enrichment.Worker.Tests/1.0"
        };
        configure?.Invoke(options);

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

    private static string CandidateJson(
        long id,
        string title,
        string artist,
        double duration,
        bool hasPlainLyrics = true,
        bool hasSyncedLyrics = false,
        bool hasLyricsfile = true,
        bool instrumental = false)
    {
        return $$"""
          {
            "id": {{id}},
            "trackName": "{{title}}",
            "artistName": "{{artist}}",
            "albumName": "Resolved Album",
            "duration": {{duration.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
            "instrumental": {{(instrumental ? "true" : "false")}},
            "hasWordSync": false,
            "hasPlainLyrics": {{(hasPlainLyrics ? "true" : "false")}},
            "hasSyncedLyrics": {{(hasSyncedLyrics ? "true" : "false")}},
            "hasLyricsfile": {{(hasLyricsfile ? "true" : "false")}}
          }
          """;
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}
