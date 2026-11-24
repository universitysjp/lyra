namespace Lyra.Catalog.Api.Services.Catalog;

using Lyra.Catalog.Api.Configuration;
using Lyra.Catalog.Api.Exceptions;
using Lyra.Catalog.Api.Repositories;
using Lyra.Catalog.Api.Services.Caching;
using Lyra.Contracts.Events;
using Microsoft.Extensions.Options;

public sealed class TrackLyricsService(
    ITrackRepository repository,
    ILibraryCache libraryCache,
    IOptions<AppStringOptions>? stringOptions = null) : ITrackLyricsService
{
    private readonly AppStringOptions _strings = stringOptions?.Value ?? new AppStringOptions();

    public async Task<bool> ApplyAsync(SongLyricsFetchedEvent lyrics, CancellationToken ct)
    {
        var result = await repository.ApplyLyricsAsync(
            lyrics.TrackId,
            lyrics.Lyrics,
            lyrics.SyncedLyrics,
            lyrics.ResolvedMetadata,
            ct) ?? throw new NotFoundException(_strings.Errors.TrackNotFound);

        if (result.Applied)
        {
            foreach (var userId in result.LinkedUserIds)
            {
                await libraryCache.InvalidateAsync(userId, ct);
            }
        }

        return result.Applied;
    }
}
