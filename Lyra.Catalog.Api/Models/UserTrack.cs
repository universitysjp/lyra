namespace Lyra.Catalog.Api.Models;

/// <summary>
/// Links a user to a track in the global catalog, carrying the user's local track identifier.
/// </summary>
public class UserTrack
{
    public Guid UserId { get; set; }

    public Guid TrackId { get; set; }

    /// <summary>The client-owned identifier for the user's local audio file.</summary>
    public string LocalTrackId { get; set; } = string.Empty;

    public DateTimeOffset ImportedAt { get; set; }

    public Track Track { get; set; } = null!;
}
