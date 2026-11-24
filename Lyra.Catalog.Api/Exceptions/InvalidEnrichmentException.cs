namespace Lyra.Catalog.Api.Exceptions;

public sealed class InvalidEnrichmentException(string message) : Exception(message)
{
}
