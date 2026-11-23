namespace Lyra.Enrichment.Worker.Models;

using System.ComponentModel;

/// <summary>Structured metadata extracted from a track's lyrics and ID3 metadata.</summary>
public sealed record TrackAnalysis(
    [property: Description("A concise primary mood, for example calm, euphoric, or melancholic.")]
    string PrimaryMood,
    [property: Description("Short thematic descriptors supported by the supplied metadata or lyrics.")]
    IReadOnlyList<string> Themes,
    [property: Description("Energy level from 1 (very calm) to 10 (very energetic).")]
    int EnergyLevel);
