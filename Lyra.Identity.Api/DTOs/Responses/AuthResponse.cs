namespace Lyra.Identity.Api.DTOs.Responses;

/// <summary>Response containing issued access and refresh tokens.</summary>
public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    Guid UserId);
