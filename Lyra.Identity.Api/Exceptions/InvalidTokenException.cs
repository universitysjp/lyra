namespace Lyra.Identity.Api.Exceptions;

public class InvalidTokenException(string message) : Exception(message)
{
}
