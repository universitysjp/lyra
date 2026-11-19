namespace Lyra.Identity.Api.Tests;

using Lyra.Contracts.Authentication;
using Lyra.Identity.Api.Data;
using Lyra.Identity.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class RefreshTokenServiceTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture _fixture;
    private readonly IOptions<JwtOptions> _jwtOptions;

    public RefreshTokenServiceTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
        _jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "https://lyra-test",
            Audience = "lyra-test-aud",
            MetadataAddress = "https://lyra-test/.well-known/openid-configuration",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 30,
            RequireHttpsMetadata = false
        });
    }

    private IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        var context = new IdentityDbContext(options);
        context.Database.Migrate();
        return context;
    }

    [Fact]
    public async Task IssueAsync_CreatesActiveTokenInDatabase()
    {
        await using var db = CreateDbContext();
        var service = new RefreshTokenService(db, _jwtOptions, NullLogger<RefreshTokenService>.Instance);
        var userId = Guid.NewGuid();

        var rawToken = await service.IssueAsync(userId, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(rawToken));
        var entity = await db.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == userId);
        Assert.NotNull(entity);
        Assert.Null(entity.RevokedAt);
        Assert.Null(entity.ReplacedByTokenId);
        Assert.True(entity.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsToken_WhenValid()
    {
        await using var db = CreateDbContext();
        var service = new RefreshTokenService(db, _jwtOptions, NullLogger<RefreshTokenService>.Instance);
        var userId = Guid.NewGuid();

        var rawToken = await service.IssueAsync(userId, CancellationToken.None);
        var validated = await service.ValidateAsync(rawToken, CancellationToken.None);

        Assert.NotNull(validated);
        Assert.Equal(userId, validated.UserId);
        Assert.True(validated.IsActive);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsNull_WhenExpired()
    {
        await using var db = CreateDbContext();
        var service = new RefreshTokenService(db, _jwtOptions, NullLogger<RefreshTokenService>.Instance);
        var userId = Guid.NewGuid();

        var rawToken = await service.IssueAsync(userId, CancellationToken.None);
        var entity = await db.RefreshTokens.FirstAsync(t => t.UserId == userId);
        entity.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        var validated = await service.ValidateAsync(rawToken, CancellationToken.None);
        Assert.Null(validated);
    }

    [Fact]
    public async Task RotateAsync_RevokesCurrentAndIssuesNewToken()
    {
        await using var db = CreateDbContext();
        var service = new RefreshTokenService(db, _jwtOptions, NullLogger<RefreshTokenService>.Instance);
        var userId = Guid.NewGuid();

        var rawToken1 = await service.IssueAsync(userId, CancellationToken.None);
        var token1 = await service.ValidateAsync(rawToken1, CancellationToken.None);
        Assert.NotNull(token1);

        var rawToken2 = await service.RotateAsync(token1, CancellationToken.None);
        Assert.NotEqual(rawToken1, rawToken2);

        // Verify token1 in db is revoked and references replacement
        var token1Db = await db.RefreshTokens.AsNoTracking().FirstAsync(t => t.Id == token1.Id);
        Assert.NotNull(token1Db.RevokedAt);
        Assert.NotNull(token1Db.ReplacedByTokenId);

        // Verify token2 is valid
        var token2 = await service.ValidateAsync(rawToken2, CancellationToken.None);
        Assert.NotNull(token2);
        Assert.Equal(token1Db.ReplacedByTokenId, token2.Id);
    }

    [Fact]
    public async Task RevokeAsync_MarksTokenRevoked_Idempotently()
    {
        await using var db = CreateDbContext();
        var service = new RefreshTokenService(db, _jwtOptions, NullLogger<RefreshTokenService>.Instance);
        var userId = Guid.NewGuid();

        var rawToken = await service.IssueAsync(userId, CancellationToken.None);
        await service.RevokeAsync(rawToken, CancellationToken.None);

        var entity = await db.RefreshTokens.FirstAsync(t => t.UserId == userId);
        Assert.NotNull(entity.RevokedAt);

        // Calling RevokeAsync again should not fail
        await service.RevokeAsync(rawToken, CancellationToken.None);
    }

    [Fact]
    public async Task ValidateAsync_WhenReusingRotatedToken_RevokesEntireFamilyAndReturnsNull()
    {
        await using var db = CreateDbContext();
        var service = new RefreshTokenService(db, _jwtOptions, NullLogger<RefreshTokenService>.Instance);
        var userId = Guid.NewGuid();

        // Issue token 1
        var rawToken1 = await service.IssueAsync(userId, CancellationToken.None);
        var token1 = await service.ValidateAsync(rawToken1, CancellationToken.None);
        Assert.NotNull(token1);

        // Rotate to token 2
        var rawToken2 = await service.RotateAsync(token1, CancellationToken.None);
        var token2 = await service.ValidateAsync(rawToken2, CancellationToken.None);
        Assert.NotNull(token2);

        // Rotate to token 3
        var rawToken3 = await service.RotateAsync(token2, CancellationToken.None);
        var token3 = await service.ValidateAsync(rawToken3, CancellationToken.None);
        Assert.NotNull(token3);

        // Reusing token 1 (already rotated) must trigger reuse detection
        var reusedResult = await service.ValidateAsync(rawToken1, CancellationToken.None);
        Assert.Null(reusedResult);

        // Entire family (including token 3) must now be revoked!
        var allTokens = await db.RefreshTokens.Where(t => t.UserId == userId).ToListAsync();
        Assert.All(allTokens, t => Assert.NotNull(t.RevokedAt));

        // Attempting to use token 3 should now fail as well
        var token3Result = await service.ValidateAsync(rawToken3, CancellationToken.None);
        Assert.Null(token3Result);
    }
}
