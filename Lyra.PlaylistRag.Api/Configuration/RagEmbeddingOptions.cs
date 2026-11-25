namespace Lyra.PlaylistRag.Api.Configuration;

public sealed class RagEmbeddingOptions
{
    public const string SectionName = "Rag";

    public string Provider { get; set; } = "OpenCode";

    public string Model { get; set; } = "text-embedding-3-small";

    public int Dimensions { get; set; } = 1536;

    public string OpenCodePlan { get; set; } = "Go";

    public string ApiKey { get; set; } = string.Empty;

    public int MaxInputCharacters { get; set; } = 8000;

    public int RequestTimeoutSeconds { get; set; } = 60;
}
