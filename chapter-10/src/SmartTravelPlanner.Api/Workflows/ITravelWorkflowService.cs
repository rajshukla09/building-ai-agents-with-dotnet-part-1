using SmartTravelPlanner.Api.Models.Execution;

namespace SmartTravelPlanner.Api.Workflows;

public interface ITravelWorkflowService
{
    Task<TripPlanResponse> ExecuteAsync(
        TravelPlanRequest request,
        CancellationToken cancellationToken = default);

    Task<TripPlanResponse> ExecuteExistingRunAsync(
        Guid runId,
        TravelPlanRequest request,
        string originalRequest,
        CancellationToken cancellationToken = default);
}
