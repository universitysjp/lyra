namespace Lyra.Identity.Api.Tests;

using Lyra.Contracts.AI;
using Lyra.Identity.Api.Configuration;
using Lyra.Identity.Api.Data;
using Lyra.Identity.Api.DTOs.Requests;
using Lyra.Identity.Api.Exceptions;
using Lyra.Identity.Api.Models;
using Lyra.Identity.Api.Services.AiConnections;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

public class UserAiConnectionServiceTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture _fixture;

    public UserAiConnectionServiceTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpsertGetAndDelete_AreUserScopedAndEncrypted()
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
        var userA = await AddUserAsync(db);
        var userB = await AddUserAsync(db);
        var service = CreateService(db);

        var apiKey = $"sk-{Guid.NewGuid():N}";
        var created = await service.UpsertAsync(
            userA,
            new UpsertAiConnectionRequest(
                AiProvider.Anthropic,
                "claude-test",
                apiKey,
                null),
            CancellationToken.None);

        Assert.Equal(AiProvider.Anthropic, created.Provider);
        db.ChangeTracker.Clear();

        var stored = await db.UserAiConnections
            .AsNoTracking()
            .SingleAsync(entry => entry.UserId == userA);
        Assert.NotEqual(apiKey, stored.ProtectedApiKey);
        Assert.DoesNotContain(apiKey, stored.ProtectedApiKey);

        var fetched = await service.GetAsync(userA, CancellationToken.None);
        Assert.Equal("claude-test", fetched.Model);

        var secret = await service.GetSecretAsync(userA, CancellationToken.None);
        Assert.NotNull(secret);
        Assert.Equal(apiKey, secret.ApiKey);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetAsync(userB, CancellationToken.None));
        Assert.Null(await service.GetSecretAsync(userB, CancellationToken.None));

        await service.UpsertAsync(
            userB,
            new UpsertAiConnectionRequest(
                AiProvider.OpenCode,
                "opencode-test",
                "oc-secret",
                OpenCodePlan.Go),
            CancellationToken.None);

        var secretA = await service.GetSecretAsync(userA, CancellationToken.None);
        var secretB = await service.GetSecretAsync(userB, CancellationToken.None);
        Assert.Equal(apiKey, secretA?.ApiKey);
        Assert.Equal(AiProvider.OpenCode, secretB?.Provider);

        await service.DeleteAsync(userA, CancellationToken.None);
        await service.DeleteAsync(userA, CancellationToken.None);

        Assert.Null(await service.GetSecretAsync(userA, CancellationToken.None));
        Assert.NotNull(await service.GetSecretAsync(userB, CancellationToken.None));
    }

    [Theory]
    [InlineData(AiProvider.OpenAI, "", "key", null)]
    [InlineData(AiProvider.OpenAI, "model", "", null)]
    [InlineData(AiProvider.OpenAI, "model", "key", OpenCodePlan.Go)]
    [InlineData(AiProvider.OpenCode, "model", "key", null)]
    public async Task Upsert_InvalidConnection_ThrowsValidationException(
        AiProvider provider,
        string model,
        string apiKey,
        OpenCodePlan? plan)
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
        var userId = await AddUserAsync(db);
        var service = CreateService(db);

        await Assert.ThrowsAsync<IdentityValidationException>(
            () => service.UpsertAsync(
                userId,
                new UpsertAiConnectionRequest(provider, model, apiKey, plan),
                CancellationToken.None));
    }

    private IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        return new IdentityDbContext(options);
    }

    private static UserAiConnectionService CreateService(IdentityDbContext db)
    {
        var strings = new AppStringOptions();
        strings.Errors.InvalidInputCode = "InvalidInput";
        strings.Errors.AiProviderInvalid = "Invalid provider.";
        strings.Errors.AiModelInvalid = "Invalid model: {0}.";
        strings.Errors.AiApiKeyInvalid = "Invalid API key: {0}.";
        strings.Errors.AiOpenCodePlanRequired = "OpenCode plan is required.";
        strings.Errors.AiOpenCodePlanNotAllowed = "OpenCode plan is not allowed.";
        strings.Errors.AiConnectionNotFound = "Not found.";

        return new UserAiConnectionService(
            db,
            new ApiKeyProtector(new EphemeralDataProtectionProvider()),
            Options.Create(new AiConnectionOptions()),
            Options.Create(strings));
    }

    private static async Task<Guid> AddUserAsync(IdentityDbContext db)
    {
        var id = Guid.NewGuid();
        var email = $"service_{id:N}@example.com";
        db.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N")
        });

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return id;
    }
}
