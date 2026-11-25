namespace Lyra.PlaylistRag.Api.Endpoints;

using System.Security.Claims;
using Lyra.Contracts.Authentication;
using Lyra.PlaylistRag.Api.DTOs.Requests;
using Lyra.PlaylistRag.Api.DTOs.Responses;
using Lyra.PlaylistRag.Api.Services.Playlists;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

public static class PlaylistEndpoints
{
    public static IEndpointRouteBuilder MapPlaylistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/playlists")
            .RequireAuthorization();

        group.MapPost("/generate", async Task<Ok<GeneratePlaylistResponse>> (
            GeneratePlaylistRequest request,
            ClaimsPrincipal principal,
            IPlaylistGenerationService generationService,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            var response = await generationService.GenerateAsync(userId, request, ct);
            return TypedResults.Ok(response);
        })
        .WithName("GeneratePlaylist")
        .WithSummary("Generate a playlist from the caller's own library")
        .Produces<GeneratePlaylistResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status502BadGateway)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }
}
