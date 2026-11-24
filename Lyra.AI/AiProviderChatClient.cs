namespace Lyra.AI;

using System.Runtime.CompilerServices;
using Lyra.AI.Configuration;
using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;

/// <summary>Applies common bounds and sanitized provider error handling.</summary>
internal sealed class AiProviderChatClient(
    IChatClient innerClient,
    AiProvider provider,
    AiProviderOptions providerOptions) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var requestCancellation = CreateRequestCancellation(cancellationToken);

        try
        {
            return await InnerClient.GetResponseAsync(
                messages,
                Bound(options),
                requestCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(exception.Message, exception, cancellationToken);
        }
        catch (Exception exception)
        {
            throw Classify(exception, cancellationToken);
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var requestCancellation = CreateRequestCancellation(cancellationToken);
        await using var enumerator = InnerClient
            .GetStreamingResponseAsync(messages, Bound(options), requestCancellation.Token)
            .GetAsyncEnumerator(requestCancellation.Token);

        while (true)
        {
            ChatResponseUpdate current;

            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    yield break;
                }

                current = enumerator.Current;
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(exception.Message, exception, cancellationToken);
            }
            catch (Exception exception)
            {
                throw Classify(exception, cancellationToken);
            }

            yield return current;
        }
    }

    private CancellationTokenSource CreateRequestCancellation(CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(providerOptions.RequestTimeout);
        return source;
    }

    private ChatOptions Bound(ChatOptions? options)
    {
        var bounded = options?.Clone() ?? new ChatOptions();
        if (bounded.MaxOutputTokens is null or <= 0)
        {
            bounded.MaxOutputTokens = providerOptions.MaxOutputTokens;
        }

        return bounded;
    }

    private AiProviderException Classify(Exception exception, CancellationToken callerToken)
    {
        if (exception is OperationCanceledException && !callerToken.IsCancellationRequested)
        {
            return new AiProviderException(
                provider,
                statusCode: null,
                isTransient: true,
                "The AI provider request timed out and can be retried.",
                exception.GetType().Name);
        }

        return AiProviderExceptionClassifier.Classify(provider, exception);
    }
}
