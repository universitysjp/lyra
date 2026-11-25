namespace Lyra.PlaylistRag.Api.Consumers;

using Lyra.Contracts.Events;
using Lyra.PlaylistRag.Api.Services.Embeddings;
using MassTransit;
using Microsoft.Extensions.Logging;

public sealed class SongEnrichedConsumer(
    ITrackEmbeddingService embeddingService,
    ILogger<SongEnrichedConsumer>? logger = null) : IConsumer<SongEnrichedEvent>
{
    public async Task Consume(ConsumeContext<SongEnrichedEvent> context)
    {
        var message = context.Message;

        if (message.TrackId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The enriched track event must contain a valid track id.");
        }

        await embeddingService.UpsertAsync(message, context.CancellationToken);

        logger?.LogInformation(
            "Stored embedding for enriched track {TrackId}.",
            message.TrackId);
    }
}
