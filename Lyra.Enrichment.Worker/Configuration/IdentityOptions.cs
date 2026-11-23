namespace Lyra.Enrichment.Worker.Configuration;

public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    public string GrpcAddress { get; set; } = "http://localhost:5004";

    public int RequestTimeoutSeconds { get; set; } = 10;
}
