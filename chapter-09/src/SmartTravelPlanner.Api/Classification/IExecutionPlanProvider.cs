namespace SmartTravelPlanner.Api.Classification;

public interface IExecutionPlanProvider
{
    Task<ExecutionPlan> CreateAsync(string request, CancellationToken cancellationToken = default);
}
