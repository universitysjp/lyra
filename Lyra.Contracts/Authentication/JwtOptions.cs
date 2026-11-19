namespace Lyra.Contracts.Authentication;

public sealed record JwtOptions
{
    public const string SectionName = "Jwt";
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string MetadataAddress { get; init; }
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 30;
    public bool RequireHttpsMetadata { get; init; } = true;
}
