namespace Lyra.Contracts.AI;

/// <summary>
/// A user's decrypted AI connection. This internal service-to-service contract
/// contains secret material and must never be published, cached, or logged.
/// </summary>
public sealed record AiConnectionSecret(
    AiProvider Provider,
    string Model,
    string ApiKey,
    OpenCodePlan? OpenCodePlan)
{
    public override string ToString() =>
        $"{nameof(AiConnectionSecret)} {{ Provider = {Provider}, Model = {Model}, " +
        $"ApiKey = [REDACTED], OpenCodePlan = {OpenCodePlan?.ToString() ?? "null"} }}";
}
