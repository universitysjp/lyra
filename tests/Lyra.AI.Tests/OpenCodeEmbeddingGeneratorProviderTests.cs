namespace Lyra.AI.Tests;

using Lyra.AI.Providers;
using Lyra.AI.Tests.TestDoubles;
using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;
using Xunit;

public class OpenCodeEmbeddingGeneratorProviderTests
{
    [Theory]
    [InlineData(
        OpenCodePlan.PayAsYouGo,
        "https://opencode.ai/inference/openai/v1/embeddings")]
    [InlineData(
        OpenCodePlan.Go,
        "https://opencode.ai/inference/go/openai/v1/embeddings")]
    public async Task Create_UsesPlanEndpointSessionHeaderAndModel(
        OpenCodePlan plan,
        string expectedEndpoint)
    {
        using var handler = new RecordingEmbeddingHttpMessageHandler();
        var provider = new OpenCodeEmbeddingGeneratorProvider(handler);
        var connection = new AiConnectionSecret(
            AiProvider.OpenCode,
            "text-embedding-3-small",
            "oc_test_key",
            plan);

        using var generator = provider.Create(connection, "ses_embed_123");
        var response = await generator.GenerateAsync(["hello"]);
        var vector = response[0].Vector;

        Assert.Equal(3, vector.Length);
        Assert.Equal(expectedEndpoint, handler.Request?.RequestUri?.AbsoluteUri);
        Assert.Equal(
            "ses_embed_123",
            Assert.Single(handler.Request!.Headers.GetValues("x-opencode-session")));
        Assert.Equal("Bearer", handler.Request.Headers.Authorization?.Scheme);
        Assert.Equal("oc_test_key", handler.Request.Headers.Authorization?.Parameter);
        Assert.Contains("\"model\":\"text-embedding-3-small\"", handler.RequestBody);
    }

    [Fact]
    public void Create_RejectsMissingPlan()
    {
        var provider = new OpenCodeEmbeddingGeneratorProvider();
        var connection = new AiConnectionSecret(
            AiProvider.OpenCode,
            "text-embedding-3-small",
            "oc_test_key",
            null);

        Assert.Throws<ArgumentException>(
            () => provider.Create(connection, "ses_embed"));
    }
}
