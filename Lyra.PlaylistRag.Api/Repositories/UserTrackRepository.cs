namespace Lyra.PlaylistRag.Api.Repositories;

using Lyra.PlaylistRag.Api.Data;
using Lyra.PlaylistRag.Api.Models;
using Microsoft.EntityFrameworkCore;

public sealed class UserTrackRepository(VectorDbContext db) : IUserTrackRepository
{
    public async Task UpsertAsync(UserTrack track, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_tracks
                ("UserId", "TrackId", "LocalTrackId", "Title", "Artist", "Album", "DurationSeconds", "ImportedAt")
            VALUES
                ({track.UserId}, {track.TrackId}, {track.LocalTrackId}, {track.Title}, {track.Artist},
                 {track.Album}, {track.DurationSeconds}, {track.ImportedAt})
            ON CONFLICT ("UserId", "TrackId") DO UPDATE SET
                "LocalTrackId" = EXCLUDED."LocalTrackId",
                "Title" = EXCLUDED."Title",
                "Artist" = EXCLUDED."Artist",
                "Album" = EXCLUDED."Album",
                "DurationSeconds" = EXCLUDED."DurationSeconds",
                "ImportedAt" = EXCLUDED."ImportedAt";
            """, ct);
    }

    public Task<UserTrack?> GetAsync(Guid userId, Guid trackId, CancellationToken ct) =>
        db.UserTracks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                track => track.UserId == userId && track.TrackId == trackId,
                ct);

    public Task<int> CountAsync(Guid userId, CancellationToken ct) =>
        db.UserTracks.CountAsync(track => track.UserId == userId, ct);
}
