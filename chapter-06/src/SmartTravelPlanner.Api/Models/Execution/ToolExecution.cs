namespace SmartTravelPlanner.Api.Models.Execution;

public sealed record ToolExecution
{
    public required int Order { get; init; }
    public required string ToolName { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public required long DurationMs { get; init; }
    public required string Status { get; init; }
    public object? Input { get; init; }
    public object? Output { get; init; }
    public string? Error { get; init; }
}
