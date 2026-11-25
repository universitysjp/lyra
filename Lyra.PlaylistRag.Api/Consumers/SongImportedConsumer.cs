namespace Lyra.PlaylistRag.Api.Consumers;

using Lyra.Contracts.Events;
using Lyra.PlaylistRag.Api.Models;
using Lyra.PlaylistRag.Api.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;

public sealed class SongImportedConsumer(
    IUserTrackRepository repository,
    ILogger<SongImportedConsumer>? logger = null) : IConsumer<SongImportedEvent>
{
    public async Task Consume(ConsumeContext<SongImportedEvent> context)
    {
        var message = context.Message;

        if (message.UserId == Guid.Empty || message.TrackId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The imported track event must contain a valid user id and track id.");
        }

        await repository.UpsertAsync(
            new UserTrack
            {
                UserId = message.UserId,
                TrackId = message.TrackId,
                LocalTrackId = message.LocalTrackId,
                Title = message.Title,
                Artist = message.Artist,
                Album = message.Album,
                DurationSeconds = message.DurationSeconds,
                ImportedAt = message.ImportedAt
            },
            context.CancellationToken);

        logger?.LogInformation(
            "Stored user track mapping for track {TrackId} and user {UserId}.",
            message.TrackId,
            message.UserId);
    }
}
