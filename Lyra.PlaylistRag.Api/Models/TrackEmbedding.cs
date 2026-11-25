namespace Lyra.PlaylistRag.Api.Models;

using Pgvector;

/// <summary>
/// The global enrichment content and vector embedding for one unique track. The same row
/// serves every user who owns the track.
/// </summary>
public sealed class TrackEmbedding
{
    /// <summary>Dimension of the deployment's embedding model.</summary>
    public const int Dimensions = 1536;

    public Guid TrackId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;

    public string? Album { get; set; }

    public int DurationSeconds { get; set; }

    public string? PrimaryMood { get; set; }

    public string[] Themes { get; set; } = [];

    public int? EnergyLevel { get; set; }

    public string SemanticSummary { get; set; } = string.Empty;

    public Vector Embedding { get; set; } = new(new float[Dimensions]);

    public string EmbeddingModel { get; set; } = string.Empty;

    public int EmbeddingDimensions { get; set; } = Dimensions;

    public DateTimeOffset EnrichedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
