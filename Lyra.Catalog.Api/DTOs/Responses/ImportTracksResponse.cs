namespace Lyra.Catalog.Api.DTOs.Responses;

/// <summary>Outcome of a batch track import.</summary>
public sealed record ImportTracksResponse(int Submitted, int Imported, int Duplicates);
