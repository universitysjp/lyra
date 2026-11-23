namespace Lyra.Enrichment.Worker.Services.Lyrics;

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lyra.Enrichment.Worker.Configuration;
using Microsoft.Extensions.Options;

public sealed class LrclibLyricsClient(
    HttpClient httpClient,
    ILrclibRequestThrottle requestThrottle,
    IOptions<LrclibOptions>? options = null) : ILyricsClient
{
    private const int MaximumSupportedDurationSeconds = 3600;

    private readonly LrclibOptions _options = options?.Value ?? new LrclibOptions();

    public async Task<string?> GetPlainLyricsAsync(
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

        if (!string.IsNullOrWhiteSpace(_options.UserAgent)
            && !httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                _options.UserAgent);
        }

        var requestPath = BuildRequestPath(title, artist, album, durationSeconds);
        await requestThrottle.WaitAsync(ct);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(
                requestPath,
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

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                var isTransient = statusCode is 408 or 429 or >= 500;
                throw new LrclibRequestException(
                    statusCode,
                    isTransient,
                    isTransient
                        ? "LRCLIB is temporarily unavailable."
                        : "LRCLIB rejected the lyrics request.");
            }

            LrclibLyricsResponse? lyrics;
            try
            {
                lyrics = await response.Content.ReadFromJsonAsync<LrclibLyricsResponse>(ct);
            }
            catch (JsonException exception)
            {
                throw new LrclibRequestException(
                    statusCode: (int)response.StatusCode,
                    isTransient: false,
                    "LRCLIB returned an invalid response.",
                    exception);
            }

            return string.IsNullOrWhiteSpace(lyrics?.PlainLyrics)
                ? null
                : lyrics.PlainLyrics;
        }
    }

    internal static string BuildRequestPath(
        string title,
        string artist,
        string? album,
        int durationSeconds)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
        {
            throw new ArgumentException("A track title and artist are required for LRCLIB.");
        }

        var parameters = new List<string>
        {
            $"track_name={Uri.EscapeDataString(title)}",
            $"artist_name={Uri.EscapeDataString(artist)}"
        };

        if (!string.IsNullOrWhiteSpace(album))
        {
            parameters.Add($"album_name={Uri.EscapeDataString(album)}");
        }

        if (durationSeconds is >= 1 and <= MaximumSupportedDurationSeconds)
        {
            parameters.Add($"duration={durationSeconds.ToString(CultureInfo.InvariantCulture)}");
        }

        return $"api/get?{string.Join('&', parameters)}";
    }
}
