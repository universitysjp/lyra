namespace Lyra.Enrichment.Worker.Services.Enrichment;

using System.Globalization;

public static class SemanticSummaryBuilder
{
    public static string Build(
        string title,
        string artist,
        string? album,
        string primaryMood,
        IReadOnlyList<string> themes,
        int energyLevel)
    {
        var albumText = string.IsNullOrWhiteSpace(album)
            ? string.Empty
            : $" Album: {album.Trim()}.";
        var themesText = themes.Count == 0
            ? "none"
            : string.Join(", ", themes);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{title.Trim()} by {artist.Trim()}.{albumText} " +
            $"Mood: {primaryMood.Trim()}. Themes: {themesText}. Energy: {energyLevel}/10.");
    }
}
