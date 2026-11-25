namespace Lyra.PlaylistRag.Api.Services.Playlists;

using Lyra.AI.Identity;
using Lyra.PlaylistRag.Api.Configuration;
using Lyra.PlaylistRag.Api.DTOs.Requests;
using Lyra.PlaylistRag.Api.DTOs.Responses;
using Lyra.PlaylistRag.Api.Exceptions;
using Lyra.PlaylistRag.Api.Services.Embeddings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Orchestrates the hard-failure playlist pipeline: user's AI connection, query plan,
/// query embedding, user-scoped vector search, validated selection, and local-id mapping.
/// </summary>
public sealed class PlaylistGenerationService(
    IUserAiConnectionClient aiConnectionClient,
    IPlaylistQueryPlanner queryPlanner,
    IPlaylistCandidateSearch candidateSearch,
    IPlaylistSelector selector,
    ITrackEmbeddingGenerator embeddingGenerator,
    IOptions<PlaylistOptions>? options = null,
    ILogger<PlaylistGenerationService>? logger = null) : IPlaylistGenerationService
{
    private readonly PlaylistOptions _options = options?.Value ?? new PlaylistOptions();

    public async Task<GeneratePlaylistResponse> GenerateAsync(
        Guid userId,
        GeneratePlaylistRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prompt = request.Prompt?.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new PlaylistValidationException("A non-empty playlist prompt is required.");
        }

        if (prompt.Length > _options.MaxPromptCharacters)
        {
            throw new PlaylistValidationException(
                $"The playlist prompt cannot exceed {_options.MaxPromptCharacters} characters.");
        }

        var limit = request.Limit ?? _options.DefaultLimit;
        if (limit < 1 || limit > _options.MaxLimit)
        {
            throw new PlaylistValidationException(
                $"The playlist limit must be between 1 and {_options.MaxLimit}.");
        }

        var connection = await aiConnectionClient.GetRequiredAsync(userId, ct);
        var sessionId = userId.ToString("N");

        var plan = await queryPlanner.PlanAsync(prompt, connection, sessionId, ct);
        var queryEmbedding = await embeddingGenerator.GenerateAsync(
            plan.SemanticQuery,
            sessionId,
            ct);

        var searchResult = await candidateSearch.SearchAsync(
            userId,
            plan,
            queryEmbedding,
            limit,
            ct);

        if (!searchResult.HasIndexedTracks)
        {
            throw new PlaylistLibraryNotReadyException();
        }

        if (searchResult.Candidates.Count == 0)
        {
            logger?.LogInformation(
                "Playlist query for user {UserId} matched no candidates after filters (prompt length {PromptLength}).",
                userId,
                prompt.Length);

            return new GeneratePlaylistResponse(
                plan.PlaylistTitle,
                plan.PlaylistDescription,
                []);
        }

        var selection = await selector.SelectAsync(
            prompt,
            plan,
            searchResult.Candidates,
            limit,
            connection,
            sessionId,
            ct);

        var tracks = new List<PlaylistTrackResponse>(selection.Tracks.Count);
        foreach (var selected in selection.Tracks)
        {
            var candidate = searchResult.Candidates[selected.Index - 1];
            tracks.Add(new PlaylistTrackResponse(
                candidate.LocalTrackId,
                candidate.Title,
                candidate.Artist,
                candidate.Album,
                candidate.DurationSeconds,
                selected.Reason));
        }

        logger?.LogInformation(
            "Generated a playlist for user {UserId} with {SelectedCount} of {CandidateCount} candidates (prompt length {PromptLength}).",
            userId,
            tracks.Count,
            searchResult.Candidates.Count,
            prompt.Length);

        return new GeneratePlaylistResponse(
            plan.PlaylistTitle,
            plan.PlaylistDescription,
            tracks);
    }
}
