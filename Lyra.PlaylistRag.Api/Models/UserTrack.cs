namespace Lyra.PlaylistRag.Api.Models;

/// <summary>
/// One user's ownership of a global track. Playlist responses return this user-specific
/// local file identifier, never a server-side track id.
/// </summary>
public sealed class UserTrack
{
    public Guid UserId { get; set; }

    public Guid TrackId { get; set; }

    public string LocalTrackId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;

    public string? Album { get; set; }

    public int DurationSeconds { get; set; }

    public DateTimeOffset ImportedAt { get; set; }
}
