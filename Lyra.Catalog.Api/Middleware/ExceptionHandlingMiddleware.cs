namespace Lyra.Catalog.Api.Middleware;

using System.Net;
using Lyra.Catalog.Api.Configuration;
using Lyra.Catalog.Api.Exceptions;
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
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, _strings.Logs.ValidationFailure, context.Request.Path, ex.Message);
            await WriteProblemAsync(
                context,
                HttpStatusCode.BadRequest,
                _strings.Titles.ValidationFailed,
                ex.Message);
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning(_strings.Logs.ResourceNotFound, context.Request.Path, ex.Message);
            await WriteProblemAsync(
                context,
                HttpStatusCode.NotFound,
                _strings.Titles.NotFound,
                ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, _strings.Logs.UnhandledException, context.Request.Path);
            await WriteProblemAsync(
                context,
                HttpStatusCode.InternalServerError,
                _strings.Titles.InternalServerError,
                null);
        }
    }

    private static Task WriteProblemAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string title,
        string? detail)
    {
        return Results.Problem(
            statusCode: (int)statusCode,
            title: title,
            detail: detail,
            instance: context.Request.Path)
            .ExecuteAsync(context);
    }
}
