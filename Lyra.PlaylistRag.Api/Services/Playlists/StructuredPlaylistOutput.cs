namespace Lyra.PlaylistRag.Api.Services.Playlists;

using System.Text.Json;
using Lyra.PlaylistRag.Api.Exceptions;
using Microsoft.Extensions.AI;

/// <summary>
/// Small wrapper around structured chat calls. Some OpenAI-compatible providers return
/// fenced JSON, an empty final message, or intermittent empty content when reasoning is
/// enabled. The wrapper lowers reasoning effort, accepts fenced JSON, retries once in plain
/// text mode, and reports provider finish reasons on failure.
/// </summary>
internal static class StructuredPlaylistOutput
{
    private const string RetryInstruction =
        "Return the result now as one valid JSON object only. " +
        "Do not include Markdown fences, commentary, or reasoning.";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    public static async Task<T> GetRequiredAsync<T>(
        IChatClient chatClient,
        IReadOnlyList<ChatMessage> messages,
        string invalidOutputMessage,
        CancellationToken ct)
    {
        var options = new ChatOptions
        {
            Reasoning = new ReasoningOptions
            {
                Effort = ReasoningEffort.Low
            }
        };

        var response = await chatClient.GetResponseAsync<T>(
            messages,
            options: options,
            useJsonSchemaResponseFormat: true,
            cancellationToken: ct);

        if (response.TryGetResult(out var result) && result is not null)
        {
            return result;
        }

        if (TryParseJson(GetRawText(response), out result) && result is not null)
        {
            return result;
        }

        // DeepSeek/OpenCode occasionally returns no assistant content on the first structured
        // request. Retry once with a short instruction and plain text mode; validation still
        // applies to the retry, and a second failure remains a hard 502.
        var retryMessages = messages
            .Concat([new ChatMessage(ChatRole.User, RetryInstruction)])
            .ToArray();
        var retryOptions = new ChatOptions
        {
            Reasoning = new ReasoningOptions
            {
                Effort = ReasoningEffort.Low
            },
            ResponseFormat = ChatResponseFormat.Text
        };

        var retry = await chatClient.GetResponseAsync(
            retryMessages,
            retryOptions,
            ct);

        if (TryParseJson(GetRawText(retry), out result) && result is not null)
        {
            return result;
        }

        var firstFinishReason = FormatFinishReason(response.FinishReason);
        var retryFinishReason = FormatFinishReason(retry.FinishReason);

        throw new PlaylistGenerationException(
            $"{invalidOutputMessage} The provider returned no parseable JSON " +
            $"(first finish reason: {firstFinishReason}; retry finish reason: {retryFinishReason}).");
    }

    private static string? GetRawText(ChatResponse response)
    {
        var content = response.Messages
            .SelectMany(message => message.Contents)
            .OfType<TextContent>()
            .Select(text => text.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();

        return content.Length > 0
            ? string.Join("\n", content)
            : response.Text;
    }

    private static string FormatFinishReason(ChatFinishReason? finishReason) =>
        finishReason is null || string.IsNullOrWhiteSpace(finishReason.Value.Value)
            ? "unknown"
            : finishReason.Value.Value;

    private static bool TryParseJson<T>(string? text, out T? result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var json = text.Trim();

        // Models sometimes wrap JSON in a Markdown code fence despite the schema request.
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = json.IndexOf('\n');
            if (firstLineEnd >= 0)
            {
                json = json[(firstLineEnd + 1)..].Trim();
            }

            if (json.EndsWith("```", StringComparison.Ordinal))
            {
                json = json[..^3].Trim();
            }
        }

        var start = json.IndexOf('{');
        var end = json.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return false;
        }

        try
        {
            result = JsonSerializer.Deserialize<T>(
                json[start..(end + 1)],
                SerializerOptions);
            return result is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
