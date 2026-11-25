namespace Lyra.AI.Providers;

using System.ClientModel;
using System.ClientModel.Primitives;
using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;
using OpenAI;

internal sealed class OpenAiEmbeddingGeneratorProvider : IAiEmbeddingGeneratorProvider
{
    private readonly HttpMessageHandler? _httpMessageHandler;

    public OpenAiEmbeddingGeneratorProvider()
    {
    }

    internal OpenAiEmbeddingGeneratorProvider(HttpMessageHandler httpMessageHandler)
    {
        _httpMessageHandler = httpMessageHandler;
    }

    public AiProvider Provider => AiProvider.OpenAI;

    public IEmbeddingGenerator<string, Embedding<float>> Create(
        AiConnectionSecret connection,
        string? sessionId)
    {
        var options = new OpenAIClientOptions();
        if (_httpMessageHandler is not null)
        {
            options.Transport = new HttpClientPipelineTransport(
                new HttpClient(_httpMessageHandler, disposeHandler: false));
        }

        var client = new OpenAIClient(new ApiKeyCredential(connection.ApiKey), options);
        return client.GetEmbeddingClient(connection.Model).AsIEmbeddingGenerator();
    }
}
