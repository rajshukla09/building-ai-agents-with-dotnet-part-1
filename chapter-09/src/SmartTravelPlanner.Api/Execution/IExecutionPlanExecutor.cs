using SmartTravelPlanner.Api.Classification;

namespace SmartTravelPlanner.Api.Execution;

public interface IExecutionPlanExecutor
{
    Task<string> EnrichRequestAsync(
        string request,
        ExecutionPlan plan,
        CancellationToken cancellationToken = default);
}
