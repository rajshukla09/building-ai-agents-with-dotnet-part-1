namespace SmartTravelPlanner.Api.Agents;

using Microsoft.Agents.AI;
using SmartTravelPlanner.Api.Context;
using ApiTripPlan = SmartTravelPlanner.Api.Models.TravelPlanning.TripPlan;
using ApiTripPlanResponse = SmartTravelPlanner.Api.Models.Execution.TripPlanResponse;
using SmartTravelPlanner.Api.Agents.Results;

public interface ITravelAgent : IApplicationAgent<TravelAgentRequest, ApiTripPlan>
{
    Task<AgentSession> CreateSessionAsync(CancellationToken cancellationToken = default);

    Task<ApiTripPlanResponse> SendMessageAsync(
        string message,
        AgentSession session,
        CancellationToken cancellationToken = default,
        TravelInvocationContext? invocationContext = null);
}
