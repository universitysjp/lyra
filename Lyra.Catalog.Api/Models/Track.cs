namespace Lyra.Catalog.Api.Models;

/// <summary>
/// A globally unique track, shared across every user who has imported the same music.
/// </summary>
public class Track
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;

    public string? Album { get; set; }

    public int DurationSeconds { get; set; }

    /// <summary>SHA-256 of the normalized title, artist and duration. Unique across the catalog.</summary>
    public string TrackFingerprint { get; set; } = string.Empty;

    public bool IsEnriched { get; set; }

    // Enrichment data populated by the enrichment milestone and shared across users.
    public string? Lyrics { get; set; }

    public string? PrimaryMood { get; set; }

    public string? Themes { get; set; }

    public int? EnergyLevel { get; set; }

    public string? SemanticSummary { get; set; }
}
