namespace SmartTravelPlanner.Api.Agents.Results;

public sealed record AgentExecutionMetadata(
    string AgentName,
    string RequestedResponseType,
    AgentExecutionStatus Status,
    int AttemptCount,
    bool StructuredDeserializationSucceeded,
    bool RawRecoveryAttempted,
    bool RawRecoverySucceeded,
    bool RegenerationAttempted,
    bool RegenerationSucceeded,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    long DurationMs,
    IReadOnlyList<string> Warnings);

public enum AgentExecutionStatus
{
    Succeeded,
    SucceededAfterRawRecovery,
    SucceededAfterRegeneration,
    FailedStructuredOutput,
    FailedValidation,
    FailedRegeneration,
    Refused,
    TimedOut,
    RateLimited,
    FailedDependency,
    FailedInfrastructure
}
