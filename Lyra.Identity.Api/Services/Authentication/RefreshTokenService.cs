namespace Lyra.Identity.Api.Services.Authentication;

using System.Security.Cryptography;
using System.Text;
using Lyra.Contracts.Authentication;
using Lyra.Identity.Api.Configuration;
using Lyra.Identity.Api.Data;
using Lyra.Identity.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class RefreshTokenService(
    IdentityDbContext dbContext,
    IOptions<JwtOptions> jwtOptions,
    ILogger<RefreshTokenService> logger,
    IOptions<AppStringOptions>? stringOptions = null) : IRefreshTokenService
{
    private readonly AppStringOptions _strings = stringOptions?.Value ?? new AppStringOptions();

    public async Task<string> IssueAsync(Guid userId, CancellationToken ct)
    {
        var rawToken = GenerateSecureToken();
        var tokenHash = HashToken(rawToken);
        var days = jwtOptions.Value.RefreshTokenDays;

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(days)
        };

        dbContext.RefreshTokens.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        return rawToken;
    }

    public async Task<RefreshToken?> ValidateAsync(string rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var tokenHash = HashToken(rawToken);
        var token = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (token is null)
        {
            return null;
        }

        if (token.RevokedAt is not null)
        {
            logger.LogWarning(_strings.Logs.TokenReuseDetected, token.UserId);
            await RevokeFamilyAsync(token.UserId, ct);
            return null;
        }

        if (token.IsExpired)
        {
            return null;
        }

        return token;
    }

    public async Task<string> RotateAsync(RefreshToken current, CancellationToken ct)
    {
        var newRawToken = GenerateSecureToken();
        var newTokenHash = HashToken(newRawToken);
        var days = jwtOptions.Value.RefreshTokenDays;

        var replacement = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = current.UserId,
            TokenHash = newTokenHash,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(days)
        };

        current.RevokedAt = DateTimeOffset.UtcNow;
        current.ReplacedByTokenId = replacement.Id;

        dbContext.RefreshTokens.Add(replacement);
        await dbContext.SaveChangesAsync(ct);

        return newRawToken;
    }

    public async Task RevokeAsync(string rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return;
        }

        var tokenHash = HashToken(rawToken);
        var token = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task RevokeFamilyAsync(Guid userId, CancellationToken ct)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        if (activeTokens.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var token in activeTokens)
            {
                token.RevokedAt = now;
            }

            await dbContext.SaveChangesAsync(ct);
        }
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
