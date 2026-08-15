namespace SmartTravelPlanner.Api.Agents.Results;

public sealed record AgentFailure(
    AgentFailureKind Kind,
    string Code,
    string Message,
    bool Retryable,
    string? Path = null,
    IReadOnlyList<AgentValidationError>? ValidationErrors = null,
    string? ProviderCode = null);

public enum AgentFailureKind
{
    StructuredOutput,
    Validation,
    Refusal,
    Timeout,
    RateLimit,
    Dependency,
    Policy,
    UnsupportedRequest,
    RegenerationFailed,
    Infrastructure
}

public sealed record AgentValidationError(string Path, string Code, string Message);
