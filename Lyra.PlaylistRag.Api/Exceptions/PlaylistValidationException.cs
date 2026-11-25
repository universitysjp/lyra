namespace Lyra.PlaylistRag.Api.Exceptions;

/// <summary>A request or query-plan value was outside the accepted bounds.</summary>
public sealed class PlaylistValidationException(string message) : Exception(message);
