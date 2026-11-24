namespace Lyra.Identity.Api.Services.Authentication;

using System.Security.Cryptography;
using Lyra.Identity.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

public sealed class RsaKeyProvider : IRsaKeyProvider, IDisposable
{
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly JsonWebKey _publicJwk;

    public string KeyId { get; }

    public RsaKeyProvider(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<RsaKeyProvider> logger,
        IOptions<AppStringOptions>? stringOptions = null)
    {
        var strings = stringOptions?.Value ?? new AppStringOptions();
        _rsa = RSA.Create(2048);

        var configuredKey = configuration["Jwt:SigningKey"];
        var devKeyPath = Path.Combine(environment.ContentRootPath, ".dev-signing-key.pem");

        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            if (File.Exists(configuredKey))
            {
                var pem = File.ReadAllText(configuredKey);
                _rsa.ImportFromPem(pem);
            }
            else
            {
                _rsa.ImportFromPem(configuredKey);
            }
        }
        else if (environment.IsDevelopment())
        {
            if (File.Exists(devKeyPath))
            {
                logger.LogInformation(strings.Logs.LoadingDevKey, devKeyPath);
                var pem = File.ReadAllText(devKeyPath);
                _rsa.ImportFromPem(pem);
            }
            else
            {
                logger.LogInformation(strings.Logs.GeneratingDevKey, devKeyPath);
                var pem = _rsa.ExportPkcs8PrivateKeyPem();
                File.WriteAllText(devKeyPath, pem);
            }
        }
        else
        {
            throw new InvalidOperationException(strings.Errors.RsaKeyNotConfigured);
        }

        var publicParameters = _rsa.ExportParameters(false);
        var configuredKeyId = configuration["Jwt:KeyId"];

        if (!string.IsNullOrWhiteSpace(configuredKeyId))
        {
            KeyId = configuredKeyId;
        }
        else if (publicParameters.Modulus is not null)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(publicParameters.Modulus);
            KeyId = Base64UrlEncoder.Encode(hash);
        }
        else
        {
            KeyId = Guid.NewGuid().ToString("N");
        }

        _signingKey = new RsaSecurityKey(_rsa)
        {
            KeyId = KeyId,
            CryptoProviderFactory = new CryptoProviderFactory
            {
                CacheSignatureProviders = false
            }
        };

        var publicRsaKey = new RsaSecurityKey(publicParameters)
        {
            KeyId = KeyId
        };

        _publicJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicRsaKey);
        _publicJwk.Use = "sig";
        _publicJwk.Alg = SecurityAlgorithms.RsaSha256;
    }

    public RsaSecurityKey GetSigningKey() => _signingKey;

    public JsonWebKey GetPublicJsonWebKey() => _publicJwk;

    public void Dispose()
    {
        _rsa.Dispose();
    }
}
