namespace Lyra.Identity.Api.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lyra.Identity.Api.Data;
using Lyra.Identity.Api.DTOs.Requests;
using Lyra.Identity.Api.DTOs.Responses;
using Lyra.Identity.Api.Services.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class AuthEndpointsTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture _fixture;

    public AuthEndpointsTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<IdentityDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<IdentityDbContext>(options =>
                {
                    options.UseNpgsql(_fixture.ConnectionString);
                });

                services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                    .Configure<IRsaKeyProvider>((options, keyProvider) =>
                    {
                        options.TokenValidationParameters.IssuerSigningKey = keyProvider.GetPublicJsonWebKey();
                    });
            });
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Database.Migrate();

        return factory;
    }

    [Fact]
    public async Task WellKnownEndpoints_ReturnJwksAndOpenIdConfig()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        // Check OpenID Configuration
        var configResponse = await client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, configResponse.StatusCode);
        var configContent = await configResponse.Content.ReadAsStringAsync();
        using var configDoc = JsonDocument.Parse(configContent);
        Assert.True(configDoc.RootElement.TryGetProperty("issuer", out var issuerProp));
        Assert.Equal("https://lyra-identity", issuerProp.GetString());
        Assert.True(configDoc.RootElement.TryGetProperty("jwks_uri", out var jwksUriProp));
        Assert.Contains("/.well-known/jwks.json", jwksUriProp.GetString());

        // Check JWKS
        var jwksResponse = await client.GetAsync("/.well-known/jwks.json");
        Assert.Equal(HttpStatusCode.OK, jwksResponse.StatusCode);
        var jwksContent = await jwksResponse.Content.ReadAsStringAsync();
        using var jwksDoc = JsonDocument.Parse(jwksContent);
        Assert.True(jwksDoc.RootElement.TryGetProperty("keys", out var keysProp));
        Assert.True(keysProp.GetArrayLength() > 0);
        var key0 = keysProp[0];
        Assert.Equal("RSA", key0.GetProperty("kty").GetString());
        Assert.Equal("sig", key0.GetProperty("use").GetString());
        Assert.Equal("RS256", key0.GetProperty("alg").GetString());
        Assert.False(string.IsNullOrWhiteSpace(key0.GetProperty("kid").GetString()));
    }

    [Fact]
    public async Task FullAuthFlow_Register_Login_Me_Refresh_Logout_Succeeds()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var email = $"user_{Guid.NewGuid():N}@example.com";
        var password = "StrongPassword123!";

        // 1. Register
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registerAuth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(registerAuth);
        Assert.False(string.IsNullOrWhiteSpace(registerAuth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(registerAuth.RefreshToken));
        Assert.NotEqual(Guid.Empty, registerAuth.UserId);

        // 2. Login
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginAuth);
        Assert.Equal(registerAuth.UserId, loginAuth.UserId);

        // 3. GET /api/auth/me with access token
        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginAuth.AccessToken);
        var meResponse = await client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(me);
        Assert.Equal(loginAuth.UserId, me.UserId);
        Assert.Equal(email, me.Email);

        // 4. Refresh token
        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(loginAuth.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshedAuth = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshedAuth);
        Assert.NotEqual(loginAuth.RefreshToken, refreshedAuth.RefreshToken);
        Assert.Equal(loginAuth.UserId, refreshedAuth.UserId);

        // 5. Logout
        var logoutResponse = await client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(refreshedAuth.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        // 6. Refresh again with revoked token should fail (401)
        var revokedRefreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(refreshedAuth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, revokedRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var email = $"duplicate_{Guid.NewGuid():N}@example.com";
        var password = "StrongPassword123!";

        var first = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var errors = await second.Content.ReadFromJsonAsync<List<IdentityError>>();
        Assert.NotNull(errors);
        Assert.Contains(errors, e => e.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_WeakPassword_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var email = $"weak_{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "weak"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await response.Content.ReadFromJsonAsync<List<IdentityError>>();
        Assert.NotNull(errors);
        Assert.True(errors.Count > 0);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var email = $"wrongpwd_{Guid.NewGuid():N}@example.com";
        var password = "StrongPassword123!";

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorizedWithoutEnumeration()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nonexistent@example.com", "Password123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
