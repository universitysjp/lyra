namespace Lyra.Enrichment.Worker.Tests;

using Lyra.Enrichment.Worker.Services.Lyrics;
using Xunit;

public class LrclibMetadataNormalizerTests
{
    [Theory]
    [InlineData("As long as you love me.. (mp3.pm)", "As long as you love me")]
    [InlineData("Song Title.mp3", "Song Title")]
    [InlineData("Song Title.FLAC", "Song Title")]
    [InlineData("Song Title [Official Video]", "Song Title")]
    [InlineData("Song Title - Official Audio", "Song Title")]
    [InlineData("  Repeated   spaces  ", "Repeated spaces")]
    [InlineData("Track (Remastered)", "Track")]
    public void NormalizeTitle_RemovesFileArtifactsAndNoise(
        string input,
        string expected)
    {
        Assert.Equal(expected, LrclibMetadataNormalizer.NormalizeTitle(input));
    }

    [Fact]
    public void ComparisonKey_IsCaseAndDiacriticInsensitive()
    {
        Assert.Equal(
            "sigur ros",
            LrclibMetadataNormalizer.ComparisonKey("  Sigur Rós "));
        Assert.Equal(
            "simon and garfunkel",
            LrclibMetadataNormalizer.ComparisonKey("Simon & Garfunkel"));
    }

    [Fact]
    public void NormalizeArtist_DoesNotDestroyMeaningfulArtistText()
    {
        Assert.Equal(
            "Bathiya & Santhush",
            LrclibMetadataNormalizer.NormalizeArtist(" Bathiya & Santhush "));
    }
}
