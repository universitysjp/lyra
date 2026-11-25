namespace Lyra.PlaylistRag.Api.Models.Playlists;

using System.ComponentModel;

/// <summary>
/// Structured interpretation of the user's request. It is produced by the user's
/// own AI connection before any vector search happens.
/// </summary>
public sealed record PlaylistQueryPlan(
    [property: Description("A concise semantic description of the requested music. Do not repeat instructions.")]
    string SemanticQuery,
    [property: Description("Mood descriptors explicitly requested by the user, or an empty list.")]
    IReadOnlyList<string> Moods,
    [property: Description("Theme descriptors explicitly requested by the user, or an empty list.")]
    IReadOnlyList<string> Themes,
    [property: Description("Minimum energy from 1 to 10, or null when the user gave no lower bound.")]
    int? MinEnergy,
    [property: Description("Maximum energy from 1 to 10, or null when the user gave no upper bound.")]
    int? MaxEnergy,
    [property: Description("An artist only when the user explicitly asked for one artist.")]
    string? Artist,
    [property: Description("A title only when the user explicitly asked for one track.")]
    string? Title,
    [property: Description("A short playlist name derived from the request.")]
    string PlaylistTitle,
    [property: Description("One or two sentences describing the playlist.")]
    string PlaylistDescription);
