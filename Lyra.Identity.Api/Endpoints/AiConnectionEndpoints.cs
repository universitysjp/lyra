namespace Lyra.Identity.Api.Endpoints;

using System.Security.Claims;
using Lyra.Contracts.Authentication;
using Lyra.Identity.Api.DTOs.Requests;
using Lyra.Identity.Api.DTOs.Responses;
using Lyra.Identity.Api.Services.AiConnections;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

public static class AiConnectionEndpoints
{
    private const string PublicGroup = "/api/auth/ai-connection";

    public static IEndpointRouteBuilder MapAiConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        var publicGroup = app.MapGroup(PublicGroup)
            .RequireAuthorization();

        publicGroup.MapGet("/", async Task<Ok<AiConnectionResponse>> (
            ClaimsPrincipal principal,
            IUserAiConnectionService service,
            CancellationToken ct) =>
        {
            var response = await service.GetAsync(principal.GetUserId(), ct);
            return TypedResults.Ok(response);
        })
        .WithName("GetAiConnection")
        .WithSummary("Get the caller's AI provider connection metadata")
        .Produces<AiConnectionResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        publicGroup.MapPut("/", async Task<Ok<AiConnectionResponse>> (
            UpsertAiConnectionRequest request,
            ClaimsPrincipal principal,
            IUserAiConnectionService service,
            CancellationToken ct) =>
        {
            var response = await service.UpsertAsync(principal.GetUserId(), request, ct);
            return TypedResults.Ok(response);
        })
        .WithName("UpsertAiConnection")
        .WithSummary("Create or replace the caller's AI provider connection")
        .Produces<AiConnectionResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        publicGroup.MapDelete("/", async Task<NoContent> (
            ClaimsPrincipal principal,
            IUserAiConnectionService service,
            CancellationToken ct) =>
        {
            await service.DeleteAsync(principal.GetUserId(), ct);
            return TypedResults.NoContent();
        })
        .WithName("DeleteAiConnection")
        .WithSummary("Delete the caller's AI provider connection")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
