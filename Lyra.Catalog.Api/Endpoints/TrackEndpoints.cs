namespace Lyra.Catalog.Api.Endpoints;

using System.Security.Claims;
using Lyra.Catalog.Api.DTOs.Requests;
using Lyra.Catalog.Api.DTOs.Responses;
using Lyra.Catalog.Api.Services.Catalog;
using Lyra.Contracts.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

public static class TrackEndpoints
{
    public static IEndpointRouteBuilder MapTrackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog/tracks")
            .RequireAuthorization();

        group.MapPost("/batch", async Task<Accepted<ImportTracksResponse>> (
            ImportTracksRequest request,
            ClaimsPrincipal principal,
            ITrackIngestionService ingestionService,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            var response = await ingestionService.ImportBatchAsync(userId, request, ct);
            return TypedResults.Accepted("/api/catalog/tracks", response);
        })
        .WithName("ImportTracksBatch")
        .WithSummary("Import a batch of local track metadata")
        .Produces<ImportTracksResponse>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/", async Task<Ok<PagedResponse<TrackResponse>>> (
            ClaimsPrincipal principal,
            ITrackQueryService queryService,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            var response = await queryService.GetLibraryAsync(
                userId, page ?? 1, pageSize ?? 0, ct);
            return TypedResults.Ok(response);
        })
        .WithName("GetTrackLibrary")
        .WithSummary("List the caller's imported tracks, newest first")
        .Produces<PagedResponse<TrackResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/search", async Task<Ok<IReadOnlyList<TrackResponse>>> (
            ClaimsPrincipal principal,
            ITrackQueryService queryService,
            string? q,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            var results = await queryService.SearchAsync(userId, q ?? string.Empty, ct);
            return TypedResults.Ok(results);
        })
        .WithName("SearchTrackLibrary")
        .WithSummary("Fuzzy-search the caller's tracks by title or artist")
        .Produces<IReadOnlyList<TrackResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{trackId:guid}/lyrics", async Task<Ok<TrackLyricsResponse>> (
            Guid trackId,
            ClaimsPrincipal principal,
            ITrackQueryService queryService,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            var lyrics = await queryService.GetLyricsAsync(userId, trackId, ct);
            return TypedResults.Ok(lyrics);
        })
        .WithName("GetTrackLyrics")
        .WithSummary("Get lyrics for one owned track")
        .Produces<TrackLyricsResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
