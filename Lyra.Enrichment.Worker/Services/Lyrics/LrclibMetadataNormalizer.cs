namespace Lyra.Enrichment.Worker.Services.Lyrics;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

internal static partial class LrclibMetadataNormalizer
{
    [GeneratedRegex(
        @"[\(\[\{][^\)\]\}]*(?:mp3\.pm|mp3|official|lyrics?|audio|video|hd|hq|4k|8d|remaster(?:ed)?|explicit|clean)[^\)\]\}]*[\)\]\}]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex NoiseBracketRegex();

    [GeneratedRegex(
        @"\s*[-–—|:]\s*(?:mp3\.pm|mp3|official(?:\s+(?:music\s+)?(?:video|audio))?|lyrics?(?:\s+video)?|audio|video|hd|hq|4k|8d(?:\s+audio)?|remaster(?:ed)?|explicit|clean)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex TrailingNoiseRegex();

    [GeneratedRegex(
        @"\.(?:mp3|flac|wav|m4a|aac|ogg|opus|wma)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex AudioExtensionRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[\._]{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex RepeatedDotsRegex();

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphanumericRegex();

    public static string NormalizeTitle(string title) => NormalizeDisplayValue(title);

    public static string NormalizeArtist(string artist) => NormalizeDisplayValue(artist);

    /// <summary>
    /// Album value usable in an exact LRCLIB query. Generic placeholders are omitted
    /// because sending them turns a valid lookup into a guaranteed miss.
    /// </summary>
    public static string? NormalizeAlbumQuery(string? album)
    {
        var normalized = NormalizeDisplayValue(album ?? string.Empty);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var key = ComparisonKey(normalized);
        return key is "unknown" or "unknown album" or "n a" or "none" or "null"
            ? null
            : normalized;
    }

    /// <summary>Aggressive form used only for local comparisons.</summary>
    public static string ComparisonKey(string value)
    {
        var normalized = NormalizeDisplayValue(value);
        var decomposed = normalized.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character == '&' ? " and " : character.ToString());
        }

        var withoutDiacritics = builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();

        return WhitespaceRegex()
            .Replace(NonAlphanumericRegex().Replace(withoutDiacritics, " "), " ")
            .Trim();
    }

    private static string NormalizeDisplayValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();

        for (var pass = 0; pass < 2; pass++)
        {
            normalized = NoiseBracketRegex().Replace(normalized, " ");
            normalized = TrailingNoiseRegex().Replace(normalized, " ");
            normalized = AudioExtensionRegex().Replace(normalized, " ");
            normalized = normalized.Trim();
        }

        normalized = RepeatedDotsRegex().Replace(normalized, " ");
        normalized = WhitespaceRegex().Replace(normalized, " ").Trim();
        return normalized.Trim(' ', '-', '_', '.', ',', ';', ':');
    }
}
