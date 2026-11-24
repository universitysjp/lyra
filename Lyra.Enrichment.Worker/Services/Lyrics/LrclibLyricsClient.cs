namespace Lyra.Enrichment.Worker.Services.Lyrics;

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lyra.Contracts.Music;
using Lyra.Enrichment.Worker.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class LrclibLyricsClient(
    HttpClient httpClient,
    ILrclibRequestThrottle requestThrottle,
    IOptions<LrclibOptions>? options = null,
    IMemoryCache? cache = null,
    ILogger<LrclibLyricsClient>? logger = null) : ILyricsClient
{
    private const int MaximumSupportedDurationSeconds = 3600;
    private const string SearchPath = "api/v2/search";

    private readonly LrclibOptions _options = options?.Value ?? new LrclibOptions();

    public async Task<LyricsResult> GetLyricsAsync(
        string title,
        string artist,
        string? album,
        int durationSeconds,
        CancellationToken ct)
    {
        if (httpClient.BaseAddress is null)
        {
            throw new InvalidOperationException("The LRCLIB HTTP base address is not configured.");
        }

        var normalizedTitle = LrclibMetadataNormalizer.NormalizeTitle(title);
        var normalizedArtist = LrclibMetadataNormalizer.NormalizeArtist(artist);
        var normalizedAlbum = LrclibMetadataNormalizer.NormalizeAlbumQuery(album);

        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            throw new ArgumentException("A track title is required for LRCLIB.");
        }

        var cacheKey = BuildCacheKey(
            normalizedTitle,
            normalizedArtist,
            normalizedAlbum,
            durationSeconds);

        if (cache?.TryGetValue(cacheKey, out CachedLyrics? cached) is true && cached is not null)
        {
            logger?.LogDebug(
                "LRCLIB lookup cache hit for {Title} by {Artist}.",
                normalizedTitle,
                normalizedArtist);
            return cached.Lyrics;
        }

        try
        {
            var result = await LookupAsync(
                normalizedTitle,
                normalizedArtist,
                normalizedAlbum,
                durationSeconds,
                ct);

            Cache(cacheKey, result);
            return result;
        }
        catch (LrclibRequestException exception)
        {
            logger?.LogWarning(
                exception,
                "LRCLIB request failed for {Title} by {Artist}; status {StatusCode}, transient={IsTransient}, retryAfter={RetryAfter}.",
                normalizedTitle,
                normalizedArtist,
                exception.StatusCode,
                exception.IsTransient,
                exception.RetryAfter);
            throw;
        }
    }

    private async Task<LyricsResult> LookupAsync(
        string title,
        string artist,
        string? album,
        int durationSeconds,
        CancellationToken ct)
    {
        LrclibLyricsResponse? exact = null;

        if (!string.IsNullOrWhiteSpace(artist))
        {
            exact = await GetRecordAsync(
                BuildRequestPath(title, artist, durationSeconds, album),
                ct);
        }

        if (exact is not null)
        {
            var exactLyrics = ExtractLyrics(exact);
            if (exactLyrics.HasLyrics)
            {
                logger?.LogInformation(
                    "LRCLIB exact lookup succeeded for {Title} by {Artist}; synced={HasSyncedLyrics}.",
                    title,
                    artist,
                    exactLyrics.SyncedLyrics is not null);
                return exactLyrics;
            }

            if (exact.Instrumental)
            {
                logger?.LogInformation(
                    "LRCLIB exact lookup matched instrumental track {Title} by {Artist}.",
                    title,
                    artist);
                return LyricsResult.Empty;
            }

            logger?.LogInformation(
                "LRCLIB exact lookup matched {Title} by {Artist} but returned no lyrics; starting candidate search.",
                title,
                artist);
        }
        else
        {
            logger?.LogInformation(
                "LRCLIB exact lookup returned 404 for {Title} by {Artist}; starting candidate search.",
                title,
                artist);
        }

        var candidates = await SearchAsync(title, artist, ct);
        if (candidates.Count == 0)
        {
            logger?.LogInformation(
                "LRCLIB v2 search returned no candidates for {Title} by {Artist}.",
                title,
                artist);
            return LyricsResult.Empty;
        }

        var evaluated = candidates
            .Where(candidate => candidate.Id > 0 && !candidate.Instrumental)
            .Select(candidate => ScoreCandidate(candidate, title, artist, durationSeconds))
            .ToList();

        logger?.LogInformation(
            "LRCLIB v2 search returned {CandidateCount} candidate(s); scored {EvaluatedCount} for {Title} by {Artist}.",
            candidates.Count,
            evaluated.Count,
            title,
            artist);

        var ranked = evaluated
            .OrderByDescending(candidate => candidate.TotalScore)
            .ThenByDescending(candidate => candidate.TitleScore)
            .ThenByDescending(candidate => candidate.DurationScore)
            .ThenByDescending(candidate => candidate.ArtistScore)
            .ThenBy(candidate => candidate.Candidate.Id)
            .Take(Math.Max(0, _options.MaxFuzzyCandidates))
            .ToList();

        var threshold = _options.FuzzyMatchThreshold;
        var acceptable = new List<ScoredCandidate>();

        foreach (var candidate in ranked)
        {
            if (candidate.TotalScore < threshold)
            {
                logger?.LogDebug(
                    "LRCLIB candidate {CandidateId} rejected for {Title}: score {Score} is below threshold {Threshold}.",
                    candidate.Candidate.Id,
                    title,
                    Math.Round(candidate.TotalScore, 2),
                    threshold);
                continue;
            }

            if (!candidate.Candidate.HasLyricsAvailability)
            {
                logger?.LogDebug(
                    "LRCLIB candidate {CandidateId} skipped for {Title}: no lyrics availability flags.",
                    candidate.Candidate.Id,
                    title);
                continue;
            }

            acceptable.Add(candidate);
        }

        if (acceptable.Count == 0)
        {
            logger?.LogInformation(
                "No LRCLIB candidate passed matching for {Title} by {Artist}; best score {BestScore}, threshold {Threshold}.",
                title,
                artist,
                ranked.Count == 0 ? 0 : Math.Round(ranked[0].TotalScore, 2),
                threshold);
            return LyricsResult.Empty;
        }

        logger?.LogInformation(
            "LRCLIB accepted {AcceptableCount} candidate(s) for {Title} by {Artist}; best score {BestScore}.",
            acceptable.Count,
            title,
            artist,
            Math.Round(acceptable[0].TotalScore, 2));

        var fetched = 0;

        foreach (var candidate in acceptable)
        {
            if (fetched >= _options.MaxCandidateDetailFetches)
            {
                logger?.LogInformation(
                    "LRCLIB stopped after {FetchCount} detail fetch(es) for {Title} by {Artist}.",
                    fetched,
                    title,
                    artist);
                break;
            }

            fetched += 1;

            var selected = await GetRecordAsync($"api/get/{candidate.Candidate.Id}", ct);
            if (selected is null)
            {
                logger?.LogWarning(
                    "LRCLIB candidate {CandidateId} for {Title} could not be fetched; trying the next candidate.",
                    candidate.Candidate.Id,
                    title);
                continue;
            }

            var selectedLyrics = ExtractLyrics(selected);
            if (!selectedLyrics.HasLyrics)
            {
                logger?.LogInformation(
                    "LRCLIB candidate {CandidateId} for {Title} returned no usable lyrics; trying the next candidate.",
                    candidate.Candidate.Id,
                    title);
                continue;
            }

            var metadata = BuildMetadata(selected, candidate.Candidate);
            logger?.LogInformation(
                "LRCLIB selected candidate {CandidateId} for {Title} by {Artist}; total={Score}, title={TitleScore}, duration={DurationScore}, artist={ArtistScore}, synced={HasSyncedLyrics}.",
                candidate.Candidate.Id,
                title,
                artist,
                Math.Round(candidate.TotalScore, 2),
                Math.Round(candidate.TitleScore, 2),
                Math.Round(candidate.DurationScore, 2),
                Math.Round(candidate.ArtistScore, 2),
                selectedLyrics.SyncedLyrics is not null);

            return selectedLyrics with { Metadata = metadata };
        }

        logger?.LogInformation(
            "No LRCLIB candidate with usable lyrics was found for {Title} by {Artist}.",
            title,
            artist);
        return LyricsResult.Empty;
    }

    private async Task<IReadOnlyList<LrclibSearchCandidate>> SearchAsync(
        string title,
        string artist,
        CancellationToken ct)
    {
        var attempts = new List<(string? Artist, string Strategy)>
        {
            (string.IsNullOrWhiteSpace(artist) ? null : artist, "full artist"),
            (PrimaryArtist(artist), "primary artist"),
            (null, "title only")
        };

        var collected = new List<LrclibSearchCandidate>();
        var seen = new HashSet<long>();
        var seenArtists = new HashSet<string?>();

        foreach (var (attemptArtist, strategy) in attempts)
        {
            if (!seenArtists.Add(attemptArtist))
            {
                continue;
            }

            var batch = await SearchOnceAsync(title, attemptArtist, ct);
            var added = 0;

            foreach (var candidate in batch)
            {
                if (seen.Add(candidate.Id))
                {
                    collected.Add(candidate);
                    added += 1;
                }
            }

            logger?.LogInformation(
                "LRCLIB v2 search with {Strategy} returned {Count} candidate(s) for {Title}; {Added} new.",
                strategy,
                batch.Count,
                title,
                added);

            if (batch.Any(candidate => candidate.HasLyricsAvailability))
            {
                break;
            }
        }

        return collected;
    }

    private async Task<IReadOnlyList<LrclibSearchCandidate>> SearchOnceAsync(
        string title,
        string? artist,
        CancellationToken ct)
    {
        var parameters = new List<string>
        {
            $"track_name={Uri.EscapeDataString(title)}"
        };

        if (!string.IsNullOrWhiteSpace(artist))
        {
            parameters.Add($"artist_name={Uri.EscapeDataString(artist)}");
        }

        var path = $"{SearchPath}?{string.Join('&', parameters)}";

        using var response = await SendAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        EnsureSuccess(response);
        return await ReadJsonAsync<List<LrclibSearchCandidate>>(response, ct) ?? [];
    }

    private async Task<LrclibLyricsResponse?> GetRecordAsync(
        string path,
        CancellationToken ct)
    {
        using var response = await SendAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response);
        return await ReadJsonAsync<LrclibLyricsResponse>(response, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(string path, CancellationToken ct)
    {
        await requestThrottle.WaitAsync(ct);

        try
        {
            return await httpClient.GetAsync(
                path,
                HttpCompletionOption.ResponseHeadersRead,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new LrclibRequestException(
                statusCode: null,
                isTransient: true,
                "The LRCLIB request timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new LrclibRequestException(
                statusCode: (int?)exception.StatusCode,
                isTransient: true,
                "The LRCLIB request failed at the network layer.",
                exception);
        }
    }

    private static async Task<T?> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
        catch (JsonException exception)
        {
            throw new LrclibRequestException(
                statusCode: (int)response.StatusCode,
                isTransient: false,
                "LRCLIB returned an invalid response.",
                exception);
        }
    }

    private void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var statusCode = (int)response.StatusCode;
        var isTransient = statusCode is 408 or 429 or >= 500;
        var retryAfter = statusCode == 429 ? GetRetryAfter(response) : null;

        if (retryAfter is not null)
        {
            requestThrottle.Pause(retryAfter.Value);
        }

        throw new LrclibRequestException(
            statusCode,
            isTransient,
            isTransient
                ? "LRCLIB is temporarily unavailable."
                : "LRCLIB rejected the lyrics request.",
            retryAfter: retryAfter);
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;

        if (header?.Delta is { } delta)
        {
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        }

        if (header?.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return null;
    }

    private static LyricsResult ExtractLyrics(LrclibLyricsResponse record)
    {
        if (record.Instrumental)
        {
            return LyricsResult.Empty;
        }

        var plainLyrics = string.IsNullOrWhiteSpace(record.PlainLyrics)
            ? null
            : record.PlainLyrics;
        var syncedLyrics = string.IsNullOrWhiteSpace(record.SyncedLyrics)
            ? null
            : record.SyncedLyrics;

        if (plainLyrics is null && syncedLyrics is null)
        {
            return LyricsResult.Empty;
        }

        return new LyricsResult(
            plainLyrics,
            syncedLyrics,
            new ResolvedTrackMetadata(
                record.TrackName,
                record.ArtistName,
                record.AlbumName,
                record.Duration,
                record.Instrumental));
    }

    private static ResolvedTrackMetadata BuildMetadata(
        LrclibLyricsResponse record,
        LrclibSearchCandidate candidate) =>
        new(
            FirstMeaningful(record.TrackName, candidate.TrackName),
            FirstMeaningful(record.ArtistName, candidate.ArtistName),
            FirstMeaningful(record.AlbumName, candidate.AlbumName),
            record.Duration is > 0
                ? record.Duration
                : candidate.Duration is > 0
                    ? candidate.Duration
                    : null,
            record.Instrumental || candidate.Instrumental);

    private static string? FirstMeaningful(string? primary, string? fallback) =>
        !string.IsNullOrWhiteSpace(primary) ? primary : fallback;

    private ScoredCandidate ScoreCandidate(
        LrclibSearchCandidate candidate,
        string targetTitle,
        string targetArtist,
        int targetDurationSeconds)
    {
        var titleScore = Math.Clamp(
            TrackTextSimilarity.TitleSimilarity(targetTitle, candidate.TrackName) * 100,
            0,
            100);
        var durationScore = Math.Clamp(
            TrackTextSimilarity.DurationSimilarity(
                targetDurationSeconds,
                candidate.Duration,
                _options.DurationToleranceSeconds) * 100,
            0,
            100);
        var artistScore = Math.Clamp(
            TrackTextSimilarity.ArtistSimilarity(targetArtist, candidate.ArtistName) * 100,
            0,
            100);

        var totalScore = Math.Clamp(
            (titleScore * 0.55) + (durationScore * 0.30) + (artistScore * 0.15),
            0,
            100);

        return new ScoredCandidate(
            candidate,
            titleScore,
            durationScore,
            artistScore,
            totalScore);
    }

    private void Cache(string key, LyricsResult lyrics)
    {
        if (cache is null)
        {
            return;
        }

        var minutes = lyrics.HasLyrics
            ? _options.SuccessCacheMinutes
            : _options.NotFoundCacheMinutes;
        if (minutes <= 0)
        {
            return;
        }

        cache.Set(
            key,
            new CachedLyrics(lyrics),
            TimeSpan.FromMinutes(minutes));
    }

    /// <summary>
    /// Extracts the first credited artist so searches like "A & B feat. C" can still
    /// match LRCLIB records that list only "A".
    /// </summary>
    private static string? PrimaryArtist(string artist)
    {
        var separators = new[]
        {
            " feat. ", " ft. ", " featuring ", " & ", ", ", " / ", " x "
        };

        var primary = artist;
        foreach (var separator in separators)
        {
            var index = primary.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                primary = primary[..index];
            }
        }

        primary = primary.Trim();
        return string.IsNullOrWhiteSpace(primary)
            || string.Equals(primary, artist, StringComparison.OrdinalIgnoreCase)
                ? null
                : primary;
    }

    internal static string BuildRequestPath(
        string title,
        string artist,
        int durationSeconds,
        string? album = null)
    {
        var normalizedTitle = LrclibMetadataNormalizer.NormalizeTitle(title);
        var normalizedArtist = LrclibMetadataNormalizer.NormalizeArtist(artist);
        var normalizedAlbum = LrclibMetadataNormalizer.NormalizeAlbumQuery(album);

        if (string.IsNullOrWhiteSpace(normalizedTitle) || string.IsNullOrWhiteSpace(normalizedArtist))
        {
            throw new ArgumentException("A track title and artist are required for LRCLIB.");
        }

        var parameters = new List<string>
        {
            $"track_name={Uri.EscapeDataString(normalizedTitle)}",
            $"artist_name={Uri.EscapeDataString(normalizedArtist)}"
        };

        if (normalizedAlbum is not null)
        {
            parameters.Add($"album_name={Uri.EscapeDataString(normalizedAlbum)}");
        }

        if (durationSeconds is >= 1 and <= MaximumSupportedDurationSeconds)
        {
            parameters.Add($"duration={durationSeconds.ToString(CultureInfo.InvariantCulture)}");
        }

        return $"api/get?{string.Join('&', parameters)}";
    }

    private static string BuildCacheKey(
        string title,
        string artist,
        string? album,
        int durationSeconds) =>
        $"lrclib:{LrclibMetadataNormalizer.ComparisonKey(title)}|" +
        $"{LrclibMetadataNormalizer.ComparisonKey(artist)}|" +
        $"{LrclibMetadataNormalizer.ComparisonKey(album ?? string.Empty)}|{durationSeconds}";

    private sealed record CachedLyrics(LyricsResult Lyrics);

    private sealed record ScoredCandidate(
        LrclibSearchCandidate Candidate,
        double TitleScore,
        double DurationScore,
        double ArtistScore,
        double TotalScore);
}
