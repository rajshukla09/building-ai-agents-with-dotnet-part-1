using SmartTravelPlanner.Api.Classification;

namespace SmartTravelPlanner.Api.Models.Execution;

public sealed record ExecutionPlanTrace
{
    public required DateTimeOffset ClassificationStartedAt
    {
        get; init;
    }
    public required DateTimeOffset ClassificationCompletedAt
    {
        get; init;
    }
    public required long ClassificationDurationMs
    {
        get; init;
    }
    public ExecutionPlan? InitialPlan
    {
        get; init;
    }
    public DateTimeOffset? ValidationStartedAt
    {
        get; init;
    }
    public DateTimeOffset? ValidationCompletedAt
    {
        get; init;
    }
    public long? ValidationDurationMs
    {
        get; init;
    }
    public required bool InitialValidationSucceeded
    {
        get; init;
    }
    public IReadOnlyList<string> InitialValidationErrors { get; init; } = [];
    public required bool RepairAttempted
    {
        get; init;
    }
    public DateTimeOffset? RepairStartedAt
    {
        get; init;
    }
    public DateTimeOffset? RepairCompletedAt
    {
        get; init;
    }
    public long? RepairDurationMs
    {
        get; init;
    }
    public ExecutionPlan? RepairedPlan
    {
        get; init;
    }
    public required bool FinalValidationSucceeded
    {
        get; init;
    }
    public IReadOnlyList<string> FinalValidationErrors { get; init; } = [];
    public ExecutionPlan? GeneratedPlan => RepairedPlan ?? InitialPlan;
    public long? ToolExecutionDurationMs
    {
        get; init;
    }
}
