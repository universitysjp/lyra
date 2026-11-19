namespace Lyra.Identity.Api.Services.Authentication;

using Lyra.Identity.Api.Models;

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(ApplicationUser user);
}
