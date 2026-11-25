namespace Lyra.PlaylistRag.Api.Middleware;

using Lyra.AI;
using Lyra.AI.Identity;
using Lyra.PlaylistRag.Api.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Maps playlist pipeline failures to problem+json. AI output validation failures are
/// bad-gateway errors; missing setup is a conflict; transient provider failures are 503.
/// No degraded or fallback playlist is ever written.
/// </summary>
public sealed class PlaylistExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<PlaylistExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away; there is no response to write.
        }
        catch (PlaylistValidationException exception)
        {
            logger.LogWarning("Playlist request validation failed: {Message}", exception.Message);
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Invalid playlist request",
                exception.Message);
        }
        catch (AiConnectionNotFoundException exception)
        {
            logger.LogWarning(exception, "Playlist generation requires an AI connection.");
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "AI provider not configured",
                "Connect an AI provider in settings before generating a playlist.");
        }
        catch (PlaylistLibraryNotReadyException exception)
        {
            logger.LogInformation(exception, "Playlist library is not indexed yet.");
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "Library is not ready",
                "No indexed tracks are available for this user yet.");
        }
        catch (PlaylistGenerationException exception)
        {
            logger.LogWarning(exception, "Playlist generation produced invalid output.");
            await WriteProblemAsync(
                context,
                StatusCodes.Status502BadGateway,
                "Playlist generation failed",
                exception.Message);
        }
        catch (AiProviderException exception)
        {
            logger.LogWarning(
                "Playlist AI provider {Provider} failed with type {ErrorType} and status {StatusCode}.",
                exception.Provider,
                exception.ErrorType,
                exception.StatusCode);

            var status = exception.IsTransient
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status502BadGateway;
            await WriteProblemAsync(
                context,
                status,
                exception.IsTransient ? "AI provider unavailable" : "AI provider rejected the request",
                exception.Message);
        }
        catch (IdentityServiceException exception)
        {
            logger.LogWarning(
                "Identity service failed with status {StatusCode} while resolving an AI connection.",
                exception.StatusCode);

            var status = exception.IsTransient
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status502BadGateway;
            await WriteProblemAsync(
                context,
                status,
                exception.IsTransient ? "Identity service unavailable" : "AI connection lookup failed",
                exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled playlist generation failure.");
            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Playlist generation failed",
                null);
        }
    }

    private static Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string? detail)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        return Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: detail,
            instance: context.Request.Path)
            .ExecuteAsync(context);
    }
}
