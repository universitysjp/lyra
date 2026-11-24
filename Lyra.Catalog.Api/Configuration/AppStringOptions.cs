namespace Lyra.Catalog.Api.Configuration;

public sealed class AppStringOptions
{
    public const string SectionName = "AppStrings";

    public ErrorStrings Errors { get; set; } = new();
    public TitleStrings Titles { get; set; } = new();
    public LogStrings Logs { get; set; } = new();

    public sealed class ErrorStrings
    {
        public string CatalogDbMissing { get; set; } = string.Empty;
        public string RedisMissing { get; set; } = string.Empty;
        public string EmptyBatch { get; set; } = string.Empty;
        public string BatchTooLarge { get; set; } = string.Empty;
        public string InvalidTrackMetadata { get; set; } = string.Empty;
        public string InvalidSearchQuery { get; set; } = string.Empty;
        public string TrackNotFound { get; set; } = string.Empty;
        public string ValidationFailed { get; set; } = string.Empty;
        public string UnhandledError { get; set; } = string.Empty;
    }

    public sealed class TitleStrings
    {
        public string ValidationFailed { get; set; } = string.Empty;
        public string NotFound { get; set; } = string.Empty;
        public string InternalServerError { get; set; } = string.Empty;
    }

    public sealed class LogStrings
    {
        public string ValidationFailure { get; set; } = string.Empty;
        public string ResourceNotFound { get; set; } = string.Empty;
        public string UnhandledException { get; set; } = string.Empty;
    }
}
