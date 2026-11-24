namespace Lyra.Identity.Api.Services.AiConnections;

public interface IInternalServiceAuthenticator
{
    bool IsValid(string? providedKey);
}
