namespace Lyra.Identity.Api.DTOs.Responses;

using System.Text.Json.Serialization;
using Lyra.Contracts.AI;

/// <summary>Non-secret metadata for the caller's AI connection.</summary>
public sealed record AiConnectionResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<AiProvider>))]
    AiProvider Provider,
    string Model,
    [property: JsonConverter(typeof(JsonStringEnumConverter<OpenCodePlan>))]
    OpenCodePlan? OpenCodePlan,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
