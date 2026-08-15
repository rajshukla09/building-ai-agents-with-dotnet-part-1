namespace SmartTravelPlanner.Api.Models.Execution;

public sealed record WorkflowExecutionTrace
{
    public required Guid WorkflowRunId
    {
        get; init;
    }
    public required DateTimeOffset StartedAt
    {
        get; init;
    }
    public required DateTimeOffset CompletedAt
    {
        get; init;
    }
    public required long DurationMs
    {
        get; init;
    }
    public required string Status
    {
        get; init;
    }
    public string? Exception
    {
        get; init;
    }
    public IReadOnlyList<ExecutorExecutionTrace> Executors { get; init; } = [];
    public IReadOnlyList<WorkflowMessageTransitionDto> MessageTransitions { get; init; } = [];
}

public sealed record ExecutorExecutionTrace
{
    public required Guid WorkflowRunId
    {
        get; init;
    }
    public required string ExecutorName
    {
        get; init;
    }
    public required string MessageType
    {
        get; init;
    }
    public string? OutputMessageType
    {
        get; init;
    }
    public required DateTimeOffset StartedAt
    {
        get; init;
    }
    public required DateTimeOffset CompletedAt
    {
        get; init;
    }
    public required long DurationMs
    {
        get; init;
    }
    public required string Status
    {
        get; init;
    }
    public string? Exception
    {
        get; init;
    }
}
