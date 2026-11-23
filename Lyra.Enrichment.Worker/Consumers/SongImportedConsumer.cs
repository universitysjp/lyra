namespace Lyra.Enrichment.Worker.Consumers;

using Lyra.Contracts.Events;
using Lyra.Enrichment.Worker.Services.Enrichment;
using MassTransit;
using Microsoft.Extensions.Logging;

public sealed class SongImportedConsumer(
    ITrackEnrichmentService enrichmentService,
    ILogger<SongImportedConsumer>? logger = null) : IConsumer<SongImportedEvent>
{
    public async Task Consume(ConsumeContext<SongImportedEvent> context)
    {
        var message = context.Message;
        var sessionId = context.MessageId?.ToString()
            ?? message.TrackId.ToString("N");

        logger?.LogInformation(
            "Processing imported track {TrackId} for user {UserId}.",
            message.TrackId,
            message.UserId);

        var enriched = await enrichmentService.EnrichAsync(
            message,
            sessionId,
            context.CancellationToken);

        await context.Publish(enriched, context.CancellationToken);
    }
}
