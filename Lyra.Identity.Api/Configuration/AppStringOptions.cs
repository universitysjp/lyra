namespace Lyra.Identity.Api.Configuration;

public sealed class AppStringOptions
{
    public const string SectionName = "AppStrings";

    public ErrorStrings Errors { get; set; } = new();
    public TitleStrings Titles { get; set; } = new();
    public LogStrings Logs { get; set; } = new();
    public EndpointStrings Endpoints { get; set; } = new();

    public sealed class ErrorStrings
    {
        public string IdentityDbMissing { get; set; } = string.Empty;
        public string EmailPasswordRequired { get; set; } = string.Empty;
        public string InvalidCredentials { get; set; } = string.Empty;
        public string UserLockedOut { get; set; } = string.Empty;
        public string InvalidOrExpiredToken { get; set; } = string.Empty;
        public string UserNotFound { get; set; } = string.Empty;
        public string RsaKeyNotConfigured { get; set; } = string.Empty;
        public string UnhandledError { get; set; } = string.Empty;
        public string ValidationFailed { get; set; } = string.Empty;
        public string InvalidInputCode { get; set; } = string.Empty;
        public string AiProviderInvalid { get; set; } = string.Empty;
        public string AiModelInvalid { get; set; } = string.Empty;
        public string AiApiKeyInvalid { get; set; } = string.Empty;
        public string AiOpenCodePlanRequired { get; set; } = string.Empty;
        public string AiOpenCodePlanNotAllowed { get; set; } = string.Empty;
        public string AiConnectionNotFound { get; set; } = string.Empty;
    }

    public sealed class TitleStrings
    {
        public string Unauthorized { get; set; } = string.Empty;
        public string AccountLocked { get; set; } = string.Empty;
        public string NotFound { get; set; } = string.Empty;
        public string InternalServerError { get; set; } = string.Empty;
    }

    public sealed class LogStrings
    {
        public string ValidationFailure { get; set; } = string.Empty;
        public string AuthenticationFailed { get; set; } = string.Empty;
        public string TokenValidationFailed { get; set; } = string.Empty;
        public string LockedOutAttempt { get; set; } = string.Empty;
        public string ResourceNotFound { get; set; } = string.Empty;
        public string UnhandledException { get; set; } = string.Empty;
        public string LoadingDevKey { get; set; } = string.Empty;
        public string GeneratingDevKey { get; set; } = string.Empty;
        public string TokenReuseDetected { get; set; } = string.Empty;
    }

    public sealed class EndpointStrings
    {
        public string MeLocation { get; set; } = string.Empty;
    }
}
