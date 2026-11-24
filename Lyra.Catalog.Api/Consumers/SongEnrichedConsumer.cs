namespace Lyra.Catalog.Api.Consumers;

using Lyra.Catalog.Api.Services.Catalog;
using Lyra.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

public sealed class SongEnrichedConsumer(
    ITrackEnrichmentService enrichmentService,
    ILogger<SongEnrichedConsumer>? logger = null) : IConsumer<SongEnrichedEvent>
{
    public async Task Consume(ConsumeContext<SongEnrichedEvent> context)
    {
        var enriched = context.Message;

        var applied = await enrichmentService.ApplyAsync(
            enriched,
            context.CancellationToken);

        logger?.LogInformation(
            "Processed enrichment for track {TrackId} from user {UserId}; applied={Applied}.",
            enriched.TrackId,
            enriched.UserId,
            applied);
    }
}
