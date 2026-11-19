namespace Lyra.Catalog.Api.DTOs.Requests;

/// <summary>A single track's locally extracted ID3 metadata.</summary>
public sealed record TrackMetadataItem(
    string LocalTrackId,
    string Title,
    string Artist,
    string? Album,
    int DurationSeconds);

/// <summary>Batch payload for importing local track metadata.</summary>
public sealed record ImportTracksRequest(IReadOnlyList<TrackMetadataItem> Tracks);
