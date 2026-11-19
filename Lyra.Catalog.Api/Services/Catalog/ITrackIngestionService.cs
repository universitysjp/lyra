namespace Lyra.Catalog.Api.Services.Catalog;

using Lyra.Catalog.Api.DTOs.Requests;
using Lyra.Catalog.Api.DTOs.Responses;

public interface ITrackIngestionService
{
    Task<ImportTracksResponse> ImportBatchAsync(
        Guid userId, ImportTracksRequest request, CancellationToken ct);
}
