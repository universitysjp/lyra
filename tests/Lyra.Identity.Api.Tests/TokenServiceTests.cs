namespace Lyra.Identity.Api.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Lyra.Contracts.Authentication;
using Lyra.Identity.Api.Models;
using Lyra.Identity.Api.Services.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

public class TokenServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly RsaKeyProvider _keyProvider;
    private readonly TokenService _tokenService;
    private readonly JwtOptions _options;

    public TokenServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "lyra-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var config = new ConfigurationBuilder().Build();
        var env = new TestHostEnvironment { ContentRootPath = _tempDir, EnvironmentName = "Development" };
        _keyProvider = new RsaKeyProvider(config, env, NullLogger<RsaKeyProvider>.Instance);

        _options = new JwtOptions
        {
            Issuer = "https://lyra-test-issuer",
            Audience = "lyra-test-aud",
            MetadataAddress = "https://lyra-test-issuer/.well-known/openid-configuration",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 30,
            RequireHttpsMetadata = false
        };

        _tokenService = new TokenService(_keyProvider, Options.Create(_options));
    }

    [Fact]
    public void CreateAccessToken_EmitsRequiredClaimsAndHeaders()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "testuser@example.com",
            UserName = "testuser@example.com"
        };

        var before = DateTimeOffset.UtcNow;
        var (tokenString, expiresAt) = _tokenService.CreateAccessToken(user);
        var after = DateTimeOffset.UtcNow;

        Assert.NotNull(tokenString);
        Assert.True(expiresAt >= before.AddMinutes(15) && expiresAt <= after.AddMinutes(15));

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        // Header assertions
        Assert.Equal("RS256", jwt.Header.Alg);
        Assert.Equal(_keyProvider.KeyId, jwt.Header.Kid);

        // Claim assertions
        Assert.Equal(_options.Issuer, jwt.Issuer);
        Assert.Contains(_options.Audience, jwt.Audiences);

        var subClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        Assert.NotNull(subClaim);
        Assert.Equal(user.Id.ToString(), subClaim.Value);

        var userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == "userId");
        Assert.NotNull(userIdClaim);
        Assert.Equal(user.Id.ToString(), userIdClaim.Value);

        var emailClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email);
        Assert.NotNull(emailClaim);
        Assert.Equal(user.Email, emailClaim.Value);

        var jtiClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        Assert.NotNull(jtiClaim);
        Assert.False(string.IsNullOrWhiteSpace(jtiClaim.Value));
    }

    [Fact]
    public void CreateAccessToken_ValidatesAgainstPublicKey()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "validate@example.com",
            UserName = "validate@example.com"
        };

        var (tokenString, _) = _tokenService.CreateAccessToken(user);

        var jwk = _keyProvider.GetPublicJsonWebKey();
        var handler = new JwtSecurityTokenHandler();

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwk,
            ClockSkew = TimeSpan.Zero
        };

        var principal = handler.ValidateToken(tokenString, validationParameters, out var validatedToken);

        Assert.NotNull(principal);
        Assert.NotNull(validatedToken);
        Assert.Equal(user.Id.ToString(), principal.Claims.First(c => c.Type == "userId" || c.Type == JwtRegisteredClaimNames.Sub).Value);
    }

    public void Dispose()
    {
        _keyProvider.Dispose();
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Lyra.Identity.Api";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
