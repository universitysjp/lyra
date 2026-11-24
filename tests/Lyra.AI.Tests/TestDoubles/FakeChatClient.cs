namespace Lyra.AI.Tests.TestDoubles;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

internal sealed class FakeChatClient(
    Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>>? responseHandler = null,
    Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, IAsyncEnumerable<ChatResponseUpdate>>? streamHandler = null)
    : IChatClient
{
    public ChatOptions? ReceivedOptions { get; private set; }

    public bool IsDisposed { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ReceivedOptions = options;
        return responseHandler?.Invoke(messages, options, cancellationToken)
            ?? Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ReceivedOptions = options;
        var updates = streamHandler?.Invoke(messages, options, cancellationToken)
            ?? EmptyStream();

        await foreach (var update in updates.WithCancellation(cancellationToken))
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() => IsDisposed = true;

    private static async IAsyncEnumerable<ChatResponseUpdate> EmptyStream()
    {
        await Task.CompletedTask;
        yield break;
    }
}
