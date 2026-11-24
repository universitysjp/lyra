namespace Lyra.AI.Providers;

using System.ClientModel.Primitives;

internal sealed class StaticHeaderPipelinePolicy(
    string headerName,
    string headerValue) : PipelinePolicy
{
    public override void Process(
        PipelineMessage message,
        IReadOnlyList<PipelinePolicy> pipeline,
        int currentIndex)
    {
        SetHeader(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(
        PipelineMessage message,
        IReadOnlyList<PipelinePolicy> pipeline,
        int currentIndex)
    {
        SetHeader(message);
        await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
    }

    private void SetHeader(PipelineMessage message) =>
        message.Request.Headers.Set(headerName, headerValue);
}
