namespace Lyra.AI;

using System.Net;
using System.Reflection;
using Lyra.Contracts.AI;

internal static class AiProviderExceptionClassifier
{
    private static readonly string[] StatusPropertyNames = ["StatusCode", "Status"];

    public static AiProviderException Classify(AiProvider provider, Exception exception)
    {
        if (exception is AiProviderException classified)
        {
            return classified;
        }

        var statusCode = FindStatusCode(exception);
        var isTransient = IsTransient(exception, statusCode);
        var message = isTransient
            ? "The AI provider is temporarily unavailable. The request can be retried."
            : "The AI provider rejected the request. Check the selected provider, model, and API key.";

        return new AiProviderException(
            provider,
            statusCode,
            isTransient,
            message,
            exception.GetType().Name);
    }

    private static bool IsTransient(Exception exception, int? statusCode)
    {
        if (statusCode is not null)
        {
            return statusCode is 408 or 429 or >= 500;
        }

        return exception is TimeoutException
            or HttpRequestException
            or IOException
            or System.Net.Sockets.SocketException
            || ContainsTransientInnerException(exception);
    }

    private static bool ContainsTransientInnerException(Exception exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException or HttpRequestException or IOException
                or System.Net.Sockets.SocketException)
            {
                return true;
            }
        }

        return false;
    }

    private static int? FindStatusCode(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException { StatusCode: { } httpStatus })
            {
                return (int)httpStatus;
            }

            foreach (var propertyName in StatusPropertyNames)
            {
                var statusProperty = current.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Public | BindingFlags.Instance);

                if (statusProperty is null)
                {
                    continue;
                }

                object? value;
                try
                {
                    value = statusProperty.GetValue(current);
                }
                catch (Exception)
                {
                    continue;
                }

                if (value is not null && TryConvertStatusCode(value, out var status))
                {
                    return status;
                }
            }
        }

        return null;
    }

    private static bool TryConvertStatusCode(object value, out int status)
    {
        switch (value)
        {
            case HttpStatusCode httpStatus:
                status = (int)httpStatus;
                return true;
            case int intStatus when intStatus is >= 100 and <= 599:
                status = intStatus;
                return true;
            case Enum enumStatus:
                status = Convert.ToInt32(enumStatus);
                return status is >= 100 and <= 599;
            default:
                status = 0;
                return false;
        }
    }
}
