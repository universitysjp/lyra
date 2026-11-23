namespace Lyra.AI;

using Lyra.Contracts.AI;

/// <summary>
/// A sanitized AI provider failure. Provider response bodies, prompts, and
/// exception details are deliberately not retained because they may contain user data.
/// </summary>
public sealed class AiProviderException(
    AiProvider provider,
    int? statusCode,
    bool isTransient,
    string message,
    string errorType) : Exception(message)
{
    public AiProvider Provider { get; } = provider;

    public int? StatusCode { get; } = statusCode;

    public bool IsTransient { get; } = isTransient;

    public string ErrorType { get; } = errorType;
}
