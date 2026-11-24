namespace Lyra.Catalog.Api.Services.Catalog;

using Lyra.Contracts.Events;

public interface ITrackLyricsService
{
    Task<bool> ApplyAsync(SongLyricsFetchedEvent lyrics, CancellationToken ct);
}
