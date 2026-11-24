namespace Lyra.Identity.Api.DTOs.Requests;

using System.Text.Json.Serialization;
using Lyra.Contracts.AI;

/// <summary>Creates or replaces the caller's AI provider connection.</summary>
public sealed record UpsertAiConnectionRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter<AiProvider>))]
    AiProvider Provider,
    string Model,
    string ApiKey,
    [property: JsonConverter(typeof(JsonStringEnumConverter<OpenCodePlan>))]
    OpenCodePlan? OpenCodePlan);
