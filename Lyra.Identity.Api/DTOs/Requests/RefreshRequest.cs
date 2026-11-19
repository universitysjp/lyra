namespace Lyra.Identity.Api.DTOs.Requests;

/// <summary>Payload for rotating a refresh token.</summary>
public sealed record RefreshRequest(string RefreshToken);
