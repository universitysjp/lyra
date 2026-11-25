namespace Lyra.PlaylistRag.Api.Repositories;

using Lyra.PlaylistRag.Api.Models;

public interface IUserTrackRepository
{
    Task UpsertAsync(UserTrack track, CancellationToken ct);

    Task<UserTrack?> GetAsync(Guid userId, Guid trackId, CancellationToken ct);

    Task<int> CountAsync(Guid userId, CancellationToken ct);
}
