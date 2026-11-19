namespace Lyra.Catalog.Api.Services.Catalog;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Computes the deterministic catalog fingerprint for a track.
/// Normalization is trim + lowercase + collapse inner whitespace.
/// </summary>
public static partial class FingerprintGenerator
{
    public static string Compute(string title, string artist, int durationSeconds)
    {
        var normalized = string.Create(
            CultureInfo.InvariantCulture,
            $"{Normalize(title)}|{Normalize(artist)}|{durationSeconds}");

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Normalize(string value) =>
        WhitespaceRegex().Replace(value.Trim().ToLowerInvariant(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
