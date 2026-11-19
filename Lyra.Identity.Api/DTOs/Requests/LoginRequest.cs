namespace Lyra.Identity.Api.DTOs.Requests;

/// <summary>Payload for authenticating an existing user.</summary>
public sealed record LoginRequest(string Email, string Password);
