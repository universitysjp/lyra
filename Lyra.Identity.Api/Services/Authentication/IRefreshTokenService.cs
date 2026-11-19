namespace Lyra.Identity.Api.Services.Authentication;

using Lyra.Identity.Api.Models;

public interface IRefreshTokenService
{
    Task<string> IssueAsync(Guid userId, CancellationToken ct);
    Task<RefreshToken?> ValidateAsync(string rawToken, CancellationToken ct);
    Task<string> RotateAsync(RefreshToken current, CancellationToken ct);
    Task RevokeAsync(string rawToken, CancellationToken ct);
    Task RevokeFamilyAsync(Guid userId, CancellationToken ct);
}
