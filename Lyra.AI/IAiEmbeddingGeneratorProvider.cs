namespace Lyra.AI;

using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;

public interface IAiEmbeddingGeneratorProvider
{
    AiProvider Provider { get; }

    IEmbeddingGenerator<string, Embedding<float>> Create(
        AiConnectionSecret connection,
        string? sessionId);
}
