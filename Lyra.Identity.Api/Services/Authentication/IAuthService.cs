namespace Lyra.Identity.Api.Services.Authentication;

using Lyra.Identity.Api.DTOs.Requests;
using Lyra.Identity.Api.DTOs.Responses;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthResponse> RefreshTokenAsync(RefreshRequest request, CancellationToken ct);
    Task RevokeTokenAsync(RefreshRequest request, CancellationToken ct);
    Task<MeResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct);
}
