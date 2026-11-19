namespace Lyra.Identity.Api.DTOs.Requests;

/// <summary>Payload for registering a new user account.</summary>
public sealed record RegisterRequest(string Email, string Password);
