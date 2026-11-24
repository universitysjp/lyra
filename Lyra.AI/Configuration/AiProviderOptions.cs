namespace Lyra.AI.Configuration;

/// <summary>Deployment-wide bounds applied to every user's AI requests.</summary>
public sealed class AiProviderOptions
{
    public const string SectionName = "AiProviders";

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(90);

    public int MaxOutputTokens { get; set; } = 2048;

    public int MaxModelLength { get; set; } = 200;

    public int MaxApiKeyLength { get; set; } = 512;
}
