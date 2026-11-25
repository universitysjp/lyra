namespace Lyra.PlaylistRag.Api.DTOs.Requests;

/// <summary>Natural-language playlist request from the mobile client.</summary>
public sealed record GeneratePlaylistRequest(string? Prompt, int? Limit);
