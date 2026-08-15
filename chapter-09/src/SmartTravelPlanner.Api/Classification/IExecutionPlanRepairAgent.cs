namespace SmartTravelPlanner.Api.Classification;

public interface IExecutionPlanRepairAgent
{
    Task<ExecutionPlan> RepairAsync(
        string originalRequest,
        ExecutionPlan invalidPlan,
        IReadOnlyList<string> validationErrors,
        CancellationToken cancellationToken = default);
}
