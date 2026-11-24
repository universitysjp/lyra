namespace Lyra.Identity.Api.Services.AiConnections;

using System.Globalization;
using Lyra.Contracts.AI;
using Lyra.Identity.Api.Configuration;
using Lyra.Identity.Api.Data;
using Lyra.Identity.Api.DTOs.Requests;
using Lyra.Identity.Api.DTOs.Responses;
using Lyra.Identity.Api.Exceptions;
using Lyra.Identity.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

public sealed class UserAiConnectionService(
    IdentityDbContext db,
    IApiKeyProtector apiKeyProtector,
    IOptions<AiConnectionOptions>? aiOptions = null,
    IOptions<AppStringOptions>? stringOptions = null) : IUserAiConnectionService
{
    private readonly AiConnectionOptions _aiOptions = aiOptions?.Value ?? new AiConnectionOptions();
    private readonly AppStringOptions _strings = stringOptions?.Value ?? new AppStringOptions();

    public async Task<AiConnectionResponse> GetAsync(Guid userId, CancellationToken ct)
    {
        var connection = await db.UserAiConnections
            .AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.UserId == userId, ct)
            ?? throw new NotFoundException(_strings.Errors.AiConnectionNotFound);

        return ToResponse(connection);
    }

    public async Task<AiConnectionResponse> UpsertAsync(
        Guid userId,
        UpsertAiConnectionRequest request,
        CancellationToken ct)
    {
        Validate(request);
        var now = DateTimeOffset.UtcNow;

        var connection = await db.UserAiConnections
            .SingleOrDefaultAsync(entry => entry.UserId == userId, ct);

        if (connection is null)
        {
            connection = new UserAiConnection
            {
                UserId = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.UserAiConnections.Add(connection);
            Apply(connection, request, now);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                db.Entry(connection).State = EntityState.Detached;
                connection = await db.UserAiConnections
                    .SingleAsync(entry => entry.UserId == userId, ct);
                Apply(connection, request, now);
                await db.SaveChangesAsync(ct);
            }

            return ToResponse(connection);
        }

        Apply(connection, request, now);
        await db.SaveChangesAsync(ct);
        return ToResponse(connection);
    }

    public async Task DeleteAsync(Guid userId, CancellationToken ct)
    {
        var connection = await db.UserAiConnections
            .SingleOrDefaultAsync(entry => entry.UserId == userId, ct);

        if (connection is null)
        {
            return;
        }

        db.UserAiConnections.Remove(connection);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AiConnectionSecret?> GetSecretAsync(Guid userId, CancellationToken ct)
    {
        var connection = await db.UserAiConnections
            .AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.UserId == userId, ct);

        if (connection is null)
        {
            return null;
        }

        return new AiConnectionSecret(
            connection.Provider,
            connection.Model,
            apiKeyProtector.Unprotect(connection.ProtectedApiKey),
            connection.OpenCodePlan);
    }

    private void Validate(UpsertAiConnectionRequest request)
    {
        if (!Enum.IsDefined(request.Provider))
        {
            throw Invalid(_strings.Errors.AiProviderInvalid);
        }

        if (string.IsNullOrWhiteSpace(request.Model)
            || request.Model.Trim().Length > _aiOptions.MaxModelLength)
        {
            throw Invalid(string.Format(
                CultureInfo.InvariantCulture,
                _strings.Errors.AiModelInvalid,
                _aiOptions.MaxModelLength));
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey)
            || request.ApiKey.Length > _aiOptions.MaxApiKeyLength)
        {
            throw Invalid(string.Format(
                CultureInfo.InvariantCulture,
                _strings.Errors.AiApiKeyInvalid,
                _aiOptions.MaxApiKeyLength));
        }

        if (request.Provider == AiProvider.OpenCode)
        {
            if (request.OpenCodePlan is null || !Enum.IsDefined(request.OpenCodePlan.Value))
            {
                throw Invalid(_strings.Errors.AiOpenCodePlanRequired);
            }
        }
        else if (request.OpenCodePlan is not null)
        {
            throw Invalid(_strings.Errors.AiOpenCodePlanNotAllowed);
        }
    }

    private void Apply(
        UserAiConnection connection,
        UpsertAiConnectionRequest request,
        DateTimeOffset now)
    {
        connection.Provider = request.Provider;
        connection.Model = request.Model.Trim();
        connection.OpenCodePlan = request.OpenCodePlan;
        connection.ProtectedApiKey = apiKeyProtector.Protect(request.ApiKey);
        connection.UpdatedAt = now;
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };

    private IdentityValidationException Invalid(string message) =>
        new(_strings.Errors.InvalidInputCode, message);

    private static AiConnectionResponse ToResponse(UserAiConnection connection) =>
        new(
            connection.Provider,
            connection.Model,
            connection.OpenCodePlan,
            connection.CreatedAt,
            connection.UpdatedAt);
}
