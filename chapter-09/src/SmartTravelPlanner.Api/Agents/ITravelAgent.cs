namespace SmartTravelPlanner.Api.Agents;

using Microsoft.Agents.AI;
using SmartTravelPlanner.Api.Contracts;
using SmartTravelPlanner.Api.Models.TravelPlanning;
using SmartTravelPlanner.Api.Models.Execution;
using SmartTravelPlanner.Api.Context;

public interface ITravelAgent
{
    Task<TripPlanResponse> CreateItineraryAsync(
        TravelPlanRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentSession> CreateSessionAsync(CancellationToken cancellationToken = default);

    Task<TripPlanResponse> SendMessageAsync(
        string message,
        AgentSession session,
        CancellationToken cancellationToken = default,
        TravelInvocationContext? invocationContext = null);
}
