namespace Lyra.Identity.Api.Services.Authentication;

using Lyra.Identity.Api.Configuration;
using Lyra.Identity.Api.DTOs.Requests;
using Lyra.Identity.Api.DTOs.Responses;
using Lyra.Identity.Api.Exceptions;
using Lyra.Identity.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IRefreshTokenService refreshTokenService,
    IOptions<AppStringOptions>? stringOptions = null) : IAuthService
{
    private readonly AppStringOptions _strings = stringOptions?.Value ?? new AppStringOptions();

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new IdentityValidationException(_strings.Errors.InvalidInputCode, _strings.Errors.EmailPasswordRequired);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new IdentityValidationException(result.Errors, _strings.Errors.ValidationFailed);
        }

        var (accessToken, expiresAt) = tokenService.CreateAccessToken(user);
        var refreshToken = await refreshTokenService.IssueAsync(user.Id, ct);
        var expiresInSeconds = (int)Math.Max(0, (expiresAt - DateTimeOffset.UtcNow).TotalSeconds);

        return new AuthResponse(accessToken, refreshToken, expiresInSeconds, user.Id);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidCredentialsException(_strings.Errors.InvalidCredentials);
        }

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            throw new InvalidCredentialsException(_strings.Errors.InvalidCredentials);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            throw new UserLockedOutException(_strings.Errors.UserLockedOut);
        }

        var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            await userManager.AccessFailedAsync(user);
            if (await userManager.IsLockedOutAsync(user))
            {
                throw new UserLockedOutException(_strings.Errors.UserLockedOut);
            }

            throw new InvalidCredentialsException(_strings.Errors.InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var (accessToken, expiresAt) = tokenService.CreateAccessToken(user);
        var refreshToken = await refreshTokenService.IssueAsync(user.Id, ct);
        var expiresInSeconds = (int)Math.Max(0, (expiresAt - DateTimeOffset.UtcNow).TotalSeconds);

        return new AuthResponse(accessToken, refreshToken, expiresInSeconds, user.Id);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshRequest request, CancellationToken ct)
    {
        var token = await refreshTokenService.ValidateAsync(request.RefreshToken, ct);
        if (token is null)
        {
            throw new InvalidTokenException(_strings.Errors.InvalidOrExpiredToken);
        }

        var user = await userManager.FindByIdAsync(token.UserId.ToString());
        if (user is null)
        {
            throw new InvalidTokenException(_strings.Errors.InvalidOrExpiredToken);
        }

        var newRefreshToken = await refreshTokenService.RotateAsync(token, ct);
        var (accessToken, expiresAt) = tokenService.CreateAccessToken(user);
        var expiresInSeconds = (int)Math.Max(0, (expiresAt - DateTimeOffset.UtcNow).TotalSeconds);

        return new AuthResponse(accessToken, newRefreshToken, expiresInSeconds, user.Id);
    }

    public async Task RevokeTokenAsync(RefreshRequest request, CancellationToken ct)
    {
        await refreshTokenService.RevokeAsync(request.RefreshToken, ct);
    }

    public async Task<MeResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new NotFoundException(_strings.Errors.UserNotFound);
        }

        return new MeResponse(user.Id, user.Email ?? string.Empty, user.CreatedAt);
    }
}
