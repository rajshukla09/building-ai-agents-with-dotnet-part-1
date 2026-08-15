namespace SmartTravelPlanner.Api.Models.Execution;

public sealed record ExecutionTrace
{
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public required long TotalDurationMs { get; init; }
    public IReadOnlyList<ToolExecution> ToolCalls { get; init; } = [];
    public IReadOnlyList<ContextProviderExecution> ContextProviders { get; init; } = [];
}
