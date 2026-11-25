namespace Lyra.PlaylistRag.Api.Configuration;

/// <summary>Bounds and defaults for the playlist generation request path.</summary>
public sealed class PlaylistOptions
{
    public const string SectionName = "Playlists";

    public int DefaultLimit { get; set; } = 20;

    public int MaxLimit { get; set; } = 50;

    public int MaxPromptCharacters { get; set; } = 2000;

    public int MaxSemanticQueryCharacters { get; set; } = 1000;

    public int MaxTitleCharacters { get; set; } = 120;

    public int MaxDescriptionCharacters { get; set; } = 600;

    public int MaxMoodCount { get; set; } = 5;

    public int MaxThemeCount { get; set; } = 8;

    public int MaxFilterValueCharacters { get; set; } = 128;

    public int MaxCandidatePool { get; set; } = 80;

    public int CandidatePoolMultiplier { get; set; } = 4;

    public int MaxCandidatesPerArtist { get; set; } = 2;

    public int MaxReasonCharacters { get; set; } = 240;
}
