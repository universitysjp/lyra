namespace Lyra.AI.Tests;

using Lyra.AI.Configuration;
using Lyra.AI.Providers;
using Lyra.AI.Tests.TestDoubles;
using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

public class AiChatClientFactoryTests
{
    [Fact]
    public void Create_ConstructsEverySupportedProvider()
    {
        var options = new AiProviderOptions();
        var factory = CreateFactory(options);

        var connections = new[]
        {
            new AiConnectionSecret(AiProvider.OpenAI, "gpt-test", "sk-openai-test", null),
            new AiConnectionSecret(AiProvider.Gemini, "gemini-test", "gemini-key-test", null),
            new AiConnectionSecret(AiProvider.OpenCode, "opencode-test", "oc_test_key", OpenCodePlan.PayAsYouGo),
            new AiConnectionSecret(AiProvider.OpenCode, "opencode-test", "oc_test_key", OpenCodePlan.Go),
            new AiConnectionSecret(AiProvider.Anthropic, "claude-test", "anthropic-key-test", null)
        };

        foreach (var connection in connections)
        {
            using var client = factory.Create(connection, "session-test");
            Assert.NotNull(client);
        }
    }

    [Fact]
    public void DependencyInjection_RegistersFactoryAndAllProviders()
    {
        var services = new ServiceCollection();
        services.AddLyraAiProviders(options =>
        {
            options.MaxOutputTokens = 321;
            options.RequestTimeout = TimeSpan.FromSeconds(12);
        });

        using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IAiChatClientFactory>();
        var connection = new AiConnectionSecret(
            AiProvider.OpenAI,
            "gpt-test",
            "sk-openai-test",
            null);

        using var client = factory.Create(connection);

        Assert.NotNull(client);
        Assert.NotNull(client.GetService<ChatClientMetadata>());
    }

    [Fact]
    public async Task Create_AppliesDefaultOutputTokenBoundWithoutMutatingCallerOptions()
    {
        var fake = new FakeChatClient();
        var factory = CreateFactory(
            new AiProviderOptions { MaxOutputTokens = 256, RequestTimeout = TimeSpan.FromSeconds(10) },
            fake);
        var callerOptions = new ChatOptions();

        using var client = factory.Create(ValidOpenAiConnection());
        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            callerOptions);

        Assert.Equal(256, fake.ReceivedOptions?.MaxOutputTokens);
        Assert.Null(callerOptions.MaxOutputTokens);
    }

    [Fact]
    public async Task Create_PreservesExplicitOutputTokenBound()
    {
        var fake = new FakeChatClient();
        var factory = CreateFactory(
            new AiProviderOptions { MaxOutputTokens = 256, RequestTimeout = TimeSpan.FromSeconds(10) },
            fake);
        var callerOptions = new ChatOptions { MaxOutputTokens = 42 };

        using var client = factory.Create(ValidOpenAiConnection());
        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            callerOptions);

        Assert.Equal(42, fake.ReceivedOptions?.MaxOutputTokens);
    }

    [Fact]
    public void Create_DisposesInnerClient()
    {
        var fake = new FakeChatClient();
        var factory = CreateFactory(new AiProviderOptions(), fake);

        var client = factory.Create(ValidOpenAiConnection());
        client.Dispose();

        Assert.True(fake.IsDisposed);
    }

    [Theory]
    [InlineData("", "sk-test")]
    [InlineData("   ", "sk-test")]
    [InlineData("model", "")]
    [InlineData("model", "   ")]
    public void Create_RejectsBlankModelOrApiKey(string model, string apiKey)
    {
        var factory = CreateFactory(new AiProviderOptions());
        var connection = new AiConnectionSecret(AiProvider.OpenAI, model, apiKey, null);

        Assert.Throws<ArgumentException>(() => factory.Create(connection));
    }

    [Fact]
    public void Create_RejectsUnsupportedProvider()
    {
        var factory = CreateFactory(new AiProviderOptions());
        var connection = new AiConnectionSecret((AiProvider)999, "model", "key", null);

        Assert.Throws<ArgumentOutOfRangeException>(() => factory.Create(connection));
    }

    [Fact]
    public void Create_RequiresOpenCodePlan()
    {
        var factory = CreateFactory(new AiProviderOptions());
        var connection = new AiConnectionSecret(AiProvider.OpenCode, "model", "key", null);

        Assert.Throws<ArgumentException>(() => factory.Create(connection, "session"));
    }

    [Fact]
    public void Create_RequiresOpenCodeSession()
    {
        var factory = CreateFactory(new AiProviderOptions());
        var connection = new AiConnectionSecret(
            AiProvider.OpenCode,
            "model",
            "key",
            OpenCodePlan.Go);

        Assert.Throws<ArgumentException>(() => factory.Create(connection));
    }

    [Fact]
    public void Create_RejectsOpenCodePlanForOtherProviders()
    {
        var factory = CreateFactory(new AiProviderOptions());
        var connection = new AiConnectionSecret(
            AiProvider.OpenAI,
            "model",
            "key",
            OpenCodePlan.Go);

        Assert.Throws<ArgumentException>(() => factory.Create(connection));
    }

    [Fact]
    public void Create_RejectsValuesAboveConfiguredBounds()
    {
        var factory = CreateFactory(new AiProviderOptions
        {
            MaxApiKeyLength = 5,
            MaxModelLength = 5
        });
        var longKey = new AiConnectionSecret(AiProvider.OpenAI, "model", "secret", null);
        var longModel = new AiConnectionSecret(AiProvider.OpenAI, "model-name", "key", null);

        Assert.Throws<ArgumentException>(() => factory.Create(longKey));
        Assert.Throws<ArgumentException>(() => factory.Create(longModel));
    }

    [Fact]
    public void Create_RejectsInvalidProviderBounds()
    {
        var factory = CreateFactory(new AiProviderOptions
        {
            RequestTimeout = TimeSpan.Zero
        });

        Assert.Throws<InvalidOperationException>(
            () => factory.Create(ValidOpenAiConnection()));
    }

    private static AiChatClientFactory CreateFactory(AiProviderOptions options) =>
        new(CreateActualProviders(options), Options.Create(options));

    private static AiChatClientFactory CreateFactory(
        AiProviderOptions options,
        FakeChatClient fake) =>
        new([new StubAiChatClientProvider(AiProvider.OpenAI, fake)], Options.Create(options));

    private static IAiChatClientProvider[] CreateActualProviders(AiProviderOptions options) =>
    [
        new OpenAiChatClientProvider(),
        new GeminiChatClientProvider(),
        new OpenCodeChatClientProvider(),
        new ClaudeChatClientProvider(Options.Create(options))
    ];

    private static AiConnectionSecret ValidOpenAiConnection() =>
        new(AiProvider.OpenAI, "gpt-test", "sk-openai-test", null);
}
