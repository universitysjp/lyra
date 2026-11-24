namespace Lyra.Catalog.Api.Consumers;

using Lyra.Catalog.Api.Services.Catalog;
using Lyra.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

public sealed class SongLyricsFetchedConsumer(
    ITrackLyricsService lyricsService,
    ILogger<SongLyricsFetchedConsumer>? logger = null) : IConsumer<SongLyricsFetchedEvent>
{
    public async Task Consume(ConsumeContext<SongLyricsFetchedEvent> context)
    {
        var lyrics = context.Message;
        if (lyrics.TrackId == Guid.Empty
            || (string.IsNullOrWhiteSpace(lyrics.Lyrics)
                && string.IsNullOrWhiteSpace(lyrics.SyncedLyrics)))
        {
            logger?.LogWarning(
                "Ignored empty lyrics event for track {TrackId} from user {UserId}.",
                lyrics.TrackId,
                lyrics.UserId);
            return;
        }

        var applied = await lyricsService.ApplyAsync(lyrics, context.CancellationToken);

        logger?.LogInformation(
            "Stored lyrics for track {TrackId} from user {UserId}; applied={Applied}.",
            lyrics.TrackId,
            lyrics.UserId,
            applied);
    }
}
