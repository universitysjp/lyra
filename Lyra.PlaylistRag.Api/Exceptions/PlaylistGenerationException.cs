namespace Lyra.PlaylistRag.Api.Exceptions;

/// <summary>
/// The generation pipeline could not produce a valid playlist. The request must fail;
/// no fallback playlist is returned.
/// </summary>
public sealed class PlaylistGenerationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
