namespace Lyra.Identity.Api.Services.Authentication;

using Microsoft.IdentityModel.Tokens;

public interface IRsaKeyProvider
{
    string KeyId { get; }
    RsaSecurityKey GetSigningKey();
    JsonWebKey GetPublicJsonWebKey();
}
