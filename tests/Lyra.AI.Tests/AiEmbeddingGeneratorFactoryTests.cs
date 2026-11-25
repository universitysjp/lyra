namespace Lyra.AI.Tests;

using Lyra.AI.Configuration;
using Lyra.AI.Providers;
using Lyra.AI.Tests.TestDoubles;
using Lyra.Contracts.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

public class AiEmbeddingGeneratorFactoryTests
{
    [Fact]
    public void DependencyInjection_RegistersEmbeddingFactoryAndProviders()
    {
        var services = new ServiceCollection();
        services.AddLyraAiProviders();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IAiEmbeddingGeneratorFactory>();
        var connection = new AiConnectionSecret(
            AiProvider.OpenCode,
            "text-embedding-3-small",
            "oc_test_key",
            OpenCodePlan.Go);

        using var generator = factory.Create(connection, "ses_embed", expectedDimensions: 1536);

        Assert.NotNull(generator);
    }

    [Fact]
    public void Create_RejectsUnsupportedProvider()
    {
        var factory = CreateFactory();
        var connection = new AiConnectionSecret(
            AiProvider.Gemini,
            "text-embedding-3-small",
            "gemini-key",
            null);

        Assert.Throws<NotSupportedException>(
            () => factory.Create(connection, "ses_embed", expectedDimensions: 1536));
    }

    [Fact]
    public void Create_RequiresOpenCodePlan()
    {
        var factory = CreateFactory();
        var connection = new AiConnectionSecret(
            AiProvider.OpenCode,
            "text-embedding-3-small",
            "oc_test_key",
            null);

        Assert.Throws<ArgumentException>(
            () => factory.Create(connection, "ses_embed", expectedDimensions: 1536));
    }

    [Fact]
    public void Create_RequiresOpenCodeSession()
    {
        var factory = CreateFactory();
        var connection = new AiConnectionSecret(
            AiProvider.OpenCode,
            "text-embedding-3-small",
            "oc_test_key",
            OpenCodePlan.Go);

        Assert.Throws<ArgumentException>(
            () => factory.Create(connection, null, expectedDimensions: 1536));
    }

    [Theory]
    [InlineData("", "key")]
    [InlineData("   ", "key")]
    [InlineData("model", "")]
    [InlineData("model", "   ")]
    public void Create_RejectsBlankModelOrApiKey(string model, string apiKey)
    {
        var factory = CreateFactory();
        var connection = new AiConnectionSecret(AiProvider.OpenAI, model, apiKey, null);

        Assert.Throws<ArgumentException>(
            () => factory.Create(connection, null, expectedDimensions: 1536));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8193)]
    public void Create_RejectsInvalidDimensions(int dimensions)
    {
        var factory = CreateFactory();
        var connection = new AiConnectionSecret(
            AiProvider.OpenAI,
            "text-embedding-3-small",
            "sk-test",
            null);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => factory.Create(connection, null, dimensions));
    }

    private static AiEmbeddingGeneratorFactory CreateFactory() =>
        new(
            [
                new OpenAiEmbeddingGeneratorProvider(),
                new OpenCodeEmbeddingGeneratorProvider()
            ],
            Options.Create(new AiProviderOptions()));
}
