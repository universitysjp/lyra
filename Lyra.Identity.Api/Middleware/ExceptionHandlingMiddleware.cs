namespace Lyra.Identity.Api.Middleware;

using System.Net;
using Lyra.Identity.Api.Configuration;
using Lyra.Identity.Api.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IOptions<AppStringOptions>? stringOptions = null)
{
    private readonly AppStringOptions _strings = stringOptions?.Value ?? new AppStringOptions();

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (IdentityValidationException ex)
        {
            logger.LogWarning(ex, _strings.Logs.ValidationFailure, context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(ex.Errors);
        }
        catch (InvalidCredentialsException ex)
        {
            logger.LogWarning(_strings.Logs.AuthenticationFailed, context.Request.Path, ex.Message);
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = (int)HttpStatusCode.Unauthorized,
                Title = _strings.Titles.Unauthorized,
                Detail = ex.Message,
                Instance = context.Request.Path
            });
        }
        catch (InvalidTokenException ex)
        {
            logger.LogWarning(_strings.Logs.TokenValidationFailed, context.Request.Path, ex.Message);
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = (int)HttpStatusCode.Unauthorized,
                Title = _strings.Titles.Unauthorized,
                Detail = ex.Message,
                Instance = context.Request.Path
            });
        }
        catch (UserLockedOutException ex)
        {
            logger.LogWarning(_strings.Logs.LockedOutAttempt, context.Request.Path, ex.Message);
            context.Response.StatusCode = StatusCodes.Status423Locked;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status423Locked,
                Title = _strings.Titles.AccountLocked,
                Detail = ex.Message,
                Instance = context.Request.Path
            });
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning(_strings.Logs.ResourceNotFound, context.Request.Path, ex.Message);
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = (int)HttpStatusCode.NotFound,
                Title = _strings.Titles.NotFound,
                Detail = ex.Message,
                Instance = context.Request.Path
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, _strings.Logs.UnhandledException, context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = _strings.Titles.InternalServerError,
                Instance = context.Request.Path
            });
        }
    }
}
