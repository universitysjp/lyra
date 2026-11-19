namespace Lyra.Identity.Api.Endpoints;

using System.Security.Claims;
using Lyra.Contracts.Authentication;
using Lyra.Identity.Api.Configuration;
using Lyra.Identity.Api.DTOs.Requests;
using Lyra.Identity.Api.DTOs.Responses;
using Lyra.Identity.Api.Services.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async Task<Created<AuthResponse>> (
            RegisterRequest request,
            IAuthService authService,
            IOptions<AppStringOptions> stringOptions,
            CancellationToken ct) =>
        {
            var response = await authService.RegisterAsync(request, ct);
            var location = stringOptions.Value.Endpoints.MeLocation;
            return TypedResults.Created(location, response);
        })
        .AllowAnonymous()
        .WithName("Register")
        .WithSummary("Register a new user account")
        .Produces<AuthResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/login", async Task<Ok<AuthResponse>> (
            LoginRequest request,
            IAuthService authService,
            CancellationToken ct) =>
        {
            var response = await authService.LoginAsync(request, ct);
            return TypedResults.Ok(response);
        })
        .AllowAnonymous()
        .WithName("Login")
        .WithSummary("Authenticate with email and password")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status423Locked);

        group.MapPost("/refresh", async Task<Ok<AuthResponse>> (
            RefreshRequest request,
            IAuthService authService,
            CancellationToken ct) =>
        {
            var response = await authService.RefreshTokenAsync(request, ct);
            return TypedResults.Ok(response);
        })
        .AllowAnonymous()
        .WithName("RefreshToken")
        .WithSummary("Rotate refresh token and issue new token pair")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async Task<NoContent> (
            RefreshRequest request,
            IAuthService authService,
            CancellationToken ct) =>
        {
            await authService.RevokeTokenAsync(request, ct);
            return TypedResults.NoContent();
        })
        .AllowAnonymous()
        .WithName("Logout")
        .WithSummary("Revoke refresh token")
        .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", async Task<Ok<MeResponse>> (
            ClaimsPrincipal principal,
            IAuthService authService,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            var response = await authService.GetCurrentUserAsync(userId, ct);
            return TypedResults.Ok(response);
        })
        .RequireAuthorization()
        .WithName("GetCurrentUser")
        .WithSummary("Get profile of current authenticated user")
        .Produces<MeResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
