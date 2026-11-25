namespace Lyra.PlaylistRag.Api.Exceptions;

/// <summary>The caller has no searchable, embedded tracks yet.</summary>
public sealed class PlaylistLibraryNotReadyException()
    : Exception("No indexed tracks are available for this user yet.");
