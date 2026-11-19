namespace Lyra.Contracts.Authentication;

using System.Security.Claims;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        if (TryGetUserId(principal, out var userId))
        {
            return userId;
        }

        throw new InvalidOperationException("User identity does not contain a valid userId claim.");
    }

    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        userId = Guid.Empty;
        var claim = principal.FindFirst("userId")
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)
            ?? principal.FindFirst("sub");

        if (claim is null || string.IsNullOrWhiteSpace(claim.Value))
        {
            return false;
        }

        return Guid.TryParse(claim.Value, out userId);
    }
}
