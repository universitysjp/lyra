namespace Lyra.AI;

using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;

public interface IAiEmbeddingGeneratorFactory
{
    IEmbeddingGenerator<string, Embedding<float>> Create(
        AiConnectionSecret connection,
        string? sessionId,
        int expectedDimensions);
}
