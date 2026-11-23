namespace Lyra.Identity.Api.Services.AiConnections;

using System.Security.Cryptography;
using System.Text;
using Lyra.Identity.Api.Configuration;
using Microsoft.Extensions.Options;

public sealed class InternalServiceAuthenticator : IInternalServiceAuthenticator
{
    private readonly byte[]? _expectedKeyHash;

    public InternalServiceAuthenticator(IOptions<InternalServicesOptions> options)
    {
        var expectedKey = options.Value.ApiKey;
        _expectedKeyHash = string.IsNullOrWhiteSpace(expectedKey)
            ? null
            : SHA256.HashData(Encoding.UTF8.GetBytes(expectedKey));
    }

    public bool IsValid(string? providedKey)
    {
        if (_expectedKeyHash is null || string.IsNullOrWhiteSpace(providedKey))
        {
            return false;
        }

        var providedKeyHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
        return CryptographicOperations.FixedTimeEquals(_expectedKeyHash, providedKeyHash);
    }
}
