namespace Lyra.Identity.Api.Services.AiConnections;

using Microsoft.AspNetCore.DataProtection;

public sealed class ApiKeyProtector : IApiKeyProtector
{
    public const string Purpose = "Lyra.Identity.UserAiConnection.v1";

    private readonly IDataProtector _protector;

    public ApiKeyProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}
