namespace Lyra.Identity.Api.DTOs.Responses;

/// <summary>Response containing current authenticated user profile details.</summary>
public sealed record MeResponse(
    Guid UserId,
    string Email,
    DateTimeOffset CreatedAt);
