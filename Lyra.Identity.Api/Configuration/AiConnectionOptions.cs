namespace Lyra.Identity.Api.Configuration;

public sealed class AiConnectionOptions
{
    public const string SectionName = "AiConnections";

    public int MaxModelLength { get; set; } = 200;

    public int MaxApiKeyLength { get; set; } = 512;
}
