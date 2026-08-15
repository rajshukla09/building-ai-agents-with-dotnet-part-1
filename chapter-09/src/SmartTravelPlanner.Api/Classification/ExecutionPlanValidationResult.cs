namespace SmartTravelPlanner.Api.Classification;

public sealed record ExecutionPlanValidationResult
{
    public required bool IsValid { get; init; }
    public required List<string> Errors { get; init; }
}
