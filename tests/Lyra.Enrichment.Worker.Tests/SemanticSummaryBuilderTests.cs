namespace Lyra.Enrichment.Worker.Tests;

using Lyra.Enrichment.Worker.Services.Enrichment;
using Xunit;

public class SemanticSummaryBuilderTests
{
    [Fact]
    public void Build_IncludesMetadataMoodThemesAndEnergy()
    {
        var summary = SemanticSummaryBuilder.Build(
            "  Song Title ",
            " Artist ",
            " Album ",
            "calm",
            ["ambient", "night"],
            3);

        Assert.Equal(
            "Song Title by Artist. Album: Album. Mood: calm. " +
            "Themes: ambient, night. Energy: 3/10.",
            summary);
    }

    [Fact]
    public void Build_OmitsAlbumAndUsesNoneForEmptyThemes()
    {
        var summary = SemanticSummaryBuilder.Build(
            "Song",
            "Artist",
            " ",
            "energetic",
            [],
            9);

        Assert.Equal(
            "Song by Artist. Mood: energetic. Themes: none. Energy: 9/10.",
            summary);
    }
}
