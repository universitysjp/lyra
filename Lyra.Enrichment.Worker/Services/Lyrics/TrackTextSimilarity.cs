namespace Lyra.Enrichment.Worker.Services.Lyrics;

internal static class TrackTextSimilarity
{
    public static double TitleSimilarity(string? left, string? right)
    {
        var normalizedLeft = LrclibMetadataNormalizer.ComparisonKey(left ?? string.Empty);
        var normalizedRight = LrclibMetadataNormalizer.ComparisonKey(right ?? string.Empty);

        return LevenshteinRatio(normalizedLeft, normalizedRight);
    }

    public static double ArtistSimilarity(string? left, string? right)
    {
        var normalizedLeft = LrclibMetadataNormalizer.ComparisonKey(left ?? string.Empty);
        var normalizedRight = LrclibMetadataNormalizer.ComparisonKey(right ?? string.Empty);

        var direct = LevenshteinRatio(normalizedLeft, normalizedRight);
        if (direct >= 1 || normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return direct;
        }

        // Multi-artist names often differ by featured artists. Containment avoids
        // rejecting a candidate that correctly names the primary artist.
        var leftTokens = normalizedLeft.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rightTokens = normalizedRight.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var common = leftTokens.Intersect(rightTokens, StringComparer.Ordinal).Count();
        var containment = common == 0
            ? 0
            : (double)common / Math.Min(leftTokens.Length, rightTokens.Length);

        return Math.Max(direct, containment);
    }

    public static double DurationSimilarity(
        int targetDurationSeconds,
        double? candidateDurationSeconds,
        double toleranceSeconds)
    {
        if (candidateDurationSeconds is null or <= 0 || targetDurationSeconds <= 0 || toleranceSeconds <= 0)
        {
            return 0;
        }

        var difference = Math.Abs(targetDurationSeconds - candidateDurationSeconds.Value);
        return Math.Max(0, 1 - (difference / toleranceSeconds));
    }

    private static double LevenshteinRatio(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return left.Length == right.Length ? 1 : 0;
        }

        if (left == right)
        {
            return 1;
        }

        if (left.Length > right.Length)
        {
            (left, right) = (right, left);
        }

        var previous = new int[left.Length + 1];
        var current = new int[left.Length + 1];

        for (var index = 0; index <= left.Length; index++)
        {
            previous[index] = index;
        }

        for (var row = 1; row <= right.Length; row++)
        {
            current[0] = row;

            for (var column = 1; column <= left.Length; column++)
            {
                var cost = left[column - 1] == right[row - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        var distance = previous[left.Length];
        return 1 - ((double)distance / right.Length);
    }
}
