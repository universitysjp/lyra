namespace Lyra.Identity.Api.Endpoints;

using Lyra.Contracts.Authentication;
using Lyra.Identity.Api.Services.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

public static class JwksEndpoints
{
    public static IEndpointRouteBuilder MapJwksEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/.well-known/jwks.json", (IRsaKeyProvider keyProvider) =>
        {
            var jwk = keyProvider.GetPublicJsonWebKey();
            return TypedResults.Ok(new { keys = new[] { jwk } });
        })
        .AllowAnonymous()
        .WithName("GetJwks")
        .WithSummary("Get JSON Web Key Set");

        app.MapGet("/.well-known/openid-configuration", (
            HttpContext context,
            IOptions<JwtOptions> jwtOptions) =>
        {
            var baseUrl = $"{context.Request.Scheme}://{context.Request.Host.Value}";
            return TypedResults.Ok(new
            {
                issuer = jwtOptions.Value.Issuer,
                jwks_uri = $"{baseUrl}/.well-known/jwks.json",
                response_types_supported = new[] { "token" },
                subject_types_supported = new[] { "public" },
                id_token_signing_alg_values_supported = new[] { "RS256" }
            });
        })
        .AllowAnonymous()
        .WithName("GetOpenIdConfiguration")
        .WithSummary("Get OpenID Connect discovery document");

        return app;
    }
}
