namespace SmartTravelPlanner.Api.Agents;

using Microsoft.Agents.AI;
using SmartTravelPlanner.Api.Contracts;
using SmartTravelPlanner.Api.Models.TravelPlanning;

public interface ITravelAgent
{
    Task<TripPlan> CreateItineraryAsync(
        TravelPlanRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentSession> CreateSessionAsync(CancellationToken cancellationToken = default);

    Task<TripPlan> SendMessageAsync(
        string message,
        AgentSession session,
        CancellationToken cancellationToken = default);

    Task<AgentSession> CloneSessionAsync(
        AgentSession session,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamMessageAsync(
        string message,
        AgentSession session,
        CancellationToken cancellationToken = default);
}
