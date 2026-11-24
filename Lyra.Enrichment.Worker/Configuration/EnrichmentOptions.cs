namespace Lyra.Enrichment.Worker.Configuration;

public sealed class EnrichmentOptions
{
    public const string SectionName = "Enrichment";

    public TimeSpan ResultCacheTtl { get; set; } = TimeSpan.FromDays(30);

    public TimeSpan LockExpiry { get; set; } = TimeSpan.FromMinutes(2);

    public int MaxLyricsCharacters { get; set; } = 12_000;
}
