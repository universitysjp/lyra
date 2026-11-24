namespace Lyra.Identity.Api.Services.AiConnections;

public interface IApiKeyProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
