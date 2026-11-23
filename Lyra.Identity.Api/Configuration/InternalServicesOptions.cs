namespace Lyra.Identity.Api.Configuration;

public sealed class InternalServicesOptions
{
    public const string SectionName = "InternalServices";

    public string ApiKey { get; set; } = string.Empty;
}
