namespace Lyra.Identity.Api.Tests;

using Lyra.Identity.Api.DTOs.Requests;
using Lyra.Identity.Api.Exceptions;
using Lyra.Identity.Api.Models;
using Lyra.Identity.Api.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Xunit;

public class AuthServiceTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture _fixture;

    public AuthServiceTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RegisterAsync_EmptyEmailOrPassword_ThrowsIdentityValidationException()
    {
        var authService = new AuthService(null!, null!, null!);

        await Assert.ThrowsAsync<IdentityValidationException>(() =>
            authService.RegisterAsync(new RegisterRequest("", "Password123!"), CancellationToken.None));

        await Assert.ThrowsAsync<IdentityValidationException>(() =>
            authService.RegisterAsync(new RegisterRequest("test@example.com", ""), CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_EmptyEmailOrPassword_ThrowsInvalidCredentialsException()
    {
        var authService = new AuthService(null!, null!, null!);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            authService.LoginAsync(new LoginRequest("", "Password123!"), CancellationToken.None));

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            authService.LoginAsync(new LoginRequest("test@example.com", ""), CancellationToken.None));
    }
}
