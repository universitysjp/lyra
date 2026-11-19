namespace Lyra.Identity.Api.Exceptions;

public class UserLockedOutException(string message) : Exception(message)
{
}
