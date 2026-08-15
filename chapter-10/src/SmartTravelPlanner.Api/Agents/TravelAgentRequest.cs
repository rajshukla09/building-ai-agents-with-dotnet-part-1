
using SmartTravelPlanner.Api.Workflows.Messages;

namespace SmartTravelPlanner.Api.Agents;

public sealed record TravelAgentRequest(
    string OriginalRequest,
    TravelPlanRequest TravelRequest,
    IReadOnlyList<ToolStepResult> ToolResults,
    string? RuntimeContext,
    string? TravelerContext);
