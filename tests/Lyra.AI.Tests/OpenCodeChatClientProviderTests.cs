namespace Lyra.AI.Tests;

using Lyra.AI.Providers;
using Lyra.AI.Tests.TestDoubles;
using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;
using Xunit;

public class OpenCodeChatClientProviderTests
{
    [Theory]
    [InlineData(
        OpenCodePlan.PayAsYouGo,
        "https://opencode.ai/inference/openai/v1/chat/completions")]
    [InlineData(
        OpenCodePlan.Go,
        "https://opencode.ai/inference/go/openai/v1/chat/completions")]
    public async Task Create_UsesPlanEndpointAndSessionHeader(
        OpenCodePlan plan,
        string expectedEndpoint)
    {
        using var handler = new RecordingHttpMessageHandler();
        var provider = new OpenCodeChatClientProvider(handler);
        var connection = new AiConnectionSecret(
            AiProvider.OpenCode,
            "opencode-test-model",
            "oc_test_key",
            plan);

        using var client = provider.Create(connection, "ses_test_123");
        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hello")]);

        Assert.Equal("ok", response.Text);
        Assert.NotNull(handler.Request);
        Assert.Equal(expectedEndpoint, handler.Request.RequestUri?.AbsoluteUri);
        Assert.Equal(
            "ses_test_123",
            Assert.Single(handler.Request.Headers.GetValues("x-opencode-session")));
        Assert.Equal("Bearer", handler.Request.Headers.Authorization?.Scheme);
        Assert.Equal("oc_test_key", handler.Request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public void Create_RejectsMissingPlan()
    {
        var provider = new OpenCodeChatClientProvider();
        var connection = new AiConnectionSecret(
            AiProvider.OpenCode,
            "opencode-test-model",
            "oc_test_key",
            null);

        Assert.Throws<ArgumentException>(
            () => provider.Create(connection, "ses_test"));
    }
}
