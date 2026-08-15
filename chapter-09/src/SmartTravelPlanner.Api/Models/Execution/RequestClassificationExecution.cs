namespace SmartTravelPlanner.Api.Models.Execution;

public sealed record RequestClassificationExecution
{
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public required long DurationMs { get; init; }
    public required string Intent { get; init; }
    public double? Confidence { get; init; }
    public required bool ValidationSucceeded { get; init; }
    public string? ValidationError { get; init; }
}
