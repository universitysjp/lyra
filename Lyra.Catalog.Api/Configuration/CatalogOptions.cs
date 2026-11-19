namespace Lyra.Catalog.Api.Configuration;

public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    public int MaxBatchSize { get; set; } = 500;

    public int DefaultPageSize { get; set; } = 50;

    public int MaxPageSize { get; set; } = 200;

    public int LibraryCacheMinutes { get; set; } = 5;

    public double SearchSimilarityThreshold { get; set; } = 0.3;
}
