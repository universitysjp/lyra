namespace Lyra.Enrichment.Worker.Services.Enrichment;

public sealed class EnrichmentOutputException(
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
}
