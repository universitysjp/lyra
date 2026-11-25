namespace Lyra.PlaylistRag.Api.Models.Playlists;

using System.ComponentModel;

/// <summary>Structured selection returned by the user's AI connection.</summary>
public sealed record PlaylistSelection(
    [property: Description("Selected candidates in playlist order. Every index must appear at most once.")]
    IReadOnlyList<PlaylistSelectionItem> Tracks);

public sealed record PlaylistSelectionItem(
    [property: Description("One-based index of the candidate to include.")]
    int Index,
    [property: Description("A short reason explaining why this candidate fits.")]
    string Reason);
