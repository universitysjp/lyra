namespace Lyra.AI.Tests;

using System.Net;
using System.Runtime.CompilerServices;
using Lyra.AI.Configuration;
using Lyra.AI.Tests.TestDoubles;
using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Xunit;

public class AiProviderErrorHandlingTests
{
    [Fact]
    public async Task RateLimit_IsClassifiedAsTransientWithoutLeakingProviderMessage()
    {
        const string providerMessage = "SECRET sk-key prompt lyrics response body";
        using var client = CreateClient(new FakeChatClient(
            (_, _, _) => throw new HttpRequestException(
                providerMessage,
                inner: null,
                HttpStatusCode.TooManyRequests)));

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => client.GetResponseAsync(Message()));

        Assert.True(exception.IsTransient);
        Assert.Equal(429, exception.StatusCode);
        Assert.Equal(nameof(HttpRequestException), exception.ErrorType);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain("sk-key", exception.Message);
        Assert.DoesNotContain("lyrics", exception.Message);
        Assert.DoesNotContain("sk-key", exception.ToString());
    }

    [Fact]
    public async Task AuthenticationFailure_IsPermanent()
    {
        using var client = CreateClient(new FakeChatClient(
            (_, _, _) => throw new HttpRequestException(
                "unauthorized",
                inner: null,
                HttpStatusCode.Unauthorized)));

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => client.GetResponseAsync(Message()));

        Assert.False(exception.IsTransient);
        Assert.Equal(401, exception.StatusCode);
    }

    [Fact]
    public async Task SdkStatusProperty_IsUsedForClassification()
    {
        using var client = CreateClient(new FakeChatClient(
            (_, _, _) => throw new ProviderStatusException(500)));

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => client.GetResponseAsync(Message()));

        Assert.True(exception.IsTransient);
        Assert.Equal(500, exception.StatusCode);
    }

    [Fact]
    public async Task ProviderTimeout_IsTransient()
    {
        var options = new AiProviderOptions
        {
            RequestTimeout = TimeSpan.FromMilliseconds(50),
            MaxOutputTokens = 64
        };

        using var client = CreateClient(
            new FakeChatClient(async (_, _, ct) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, "unreachable"));
            }),
            options);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => client.GetResponseAsync(Message()));

        Assert.True(exception.IsTransient);
        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CallerCancellation_IsNotConvertedToProviderFailure()
    {
        using var client = CreateClient(new FakeChatClient(async (_, _, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "unreachable"));
        }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetResponseAsync(Message(), cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task StreamingFailure_IsClassifiedAfterSuccessfulChunks()
    {
        using var client = CreateClient(new FakeChatClient(
            streamHandler: (_, _, _) => StreamThenFail()));

        await using var enumerator = client
            .GetStreamingResponseAsync(Message())
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("first", enumerator.Current.Text);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            async () => await enumerator.MoveNextAsync());

        Assert.True(exception.IsTransient);
        Assert.Equal(503, exception.StatusCode);
    }

    private static IChatClient CreateClient(
        FakeChatClient fake,
        AiProviderOptions? options = null)
    {
        options ??= new AiProviderOptions
        {
            RequestTimeout = TimeSpan.FromSeconds(5),
            MaxOutputTokens = 64
        };

        var factory = new AiChatClientFactory(
            [new StubAiChatClientProvider(AiProvider.OpenAI, fake)],
            Options.Create(options));

        return factory.Create(
            new AiConnectionSecret(AiProvider.OpenAI, "gpt-test", "sk-test", null));
    }

    private static ChatMessage Message() => new(ChatRole.User, "Enrich this song");

    private static async IAsyncEnumerable<ChatResponseUpdate> StreamThenFail(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, "first");

        await Task.Yield();
        throw new HttpRequestException(
            "provider unavailable",
            inner: null,
            HttpStatusCode.ServiceUnavailable);
    }

    private sealed class ProviderStatusException(int status) : Exception("provider status test")
    {
        public int Status { get; } = status;
    }
}
