namespace Lyra.Catalog.Api.Tests;

using Lyra.Catalog.Api.Services.Catalog;
using Xunit;

public class FingerprintGeneratorTests
{
    [Fact]
    public void Compute_NormalizesCaseAndWhitespace()
    {
        var first = FingerprintGenerator.Compute(" The  Song ", "The   Artist", 210);
        var second = FingerprintGenerator.Compute("the song", "the artist", 210);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_IsDeterministicAndLowercaseHex()
    {
        var first = FingerprintGenerator.Compute("Bohemian Rhapsody", "Queen", 355);
        var second = FingerprintGenerator.Compute("Bohemian Rhapsody", "Queen", 355);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    [Fact]
    public void Compute_DifferentDuration_ProducesDifferentFingerprint()
    {
        var first = FingerprintGenerator.Compute("Bohemian Rhapsody", "Queen", 355);
        var second = FingerprintGenerator.Compute("Bohemian Rhapsody", "Queen", 356);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Compute_DifferentArtist_ProducesDifferentFingerprint()
    {
        var first = FingerprintGenerator.Compute("Bohemian Rhapsody", "Queen", 355);
        var second = FingerprintGenerator.Compute("Bohemian Rhapsody", "Queen Tribute", 355);

        Assert.NotEqual(first, second);
    }
}
