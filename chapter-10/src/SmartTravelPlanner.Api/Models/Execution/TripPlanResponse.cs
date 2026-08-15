using SmartTravelPlanner.Api.Models.TravelPlanning;

namespace SmartTravelPlanner.Api.Models.Execution;

public sealed record TripPlanResponse(
    TripPlan TripPlan,
    ExecutionTrace Execution,
    WorkflowExecutionTrace? Workflow = null);
