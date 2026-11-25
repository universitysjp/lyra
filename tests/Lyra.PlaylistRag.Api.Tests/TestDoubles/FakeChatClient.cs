namespace Lyra.PlaylistRag.Api.Tests.TestDoubles;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

internal sealed class FakeChatClient(
    Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> handler)
    : IChatClient
{
    public IReadOnlyList<ChatMessage>? Messages { get; private set; }

    public ChatOptions? Options { get; private set; }

    public int Calls { get; private set; }

    public bool IsDisposed { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        Messages = messages.ToList();
        Options = options;
        return handler(Messages, options, cancellationToken);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() => IsDisposed = true;
}
