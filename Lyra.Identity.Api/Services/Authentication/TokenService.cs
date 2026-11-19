namespace Lyra.Identity.Api.Services.Authentication;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Lyra.Contracts.Authentication;
using Lyra.Identity.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

public sealed class TokenService(
    IRsaKeyProvider rsaKeyProvider,
    IOptions<JwtOptions> jwtOptions) : ITokenService
{
    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(ApplicationUser user)
    {
        var options = jwtOptions.Value;
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(options.AccessTokenMinutes);

        var signingKey = rsaKeyProvider.GetSigningKey();
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("userId", user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        var jwt = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(jwt);

        return (tokenString, expiresAt);
    }
}
