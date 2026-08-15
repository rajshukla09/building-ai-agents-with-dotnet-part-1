namespace SmartTravelPlanner.Api.Agents;

using SmartTravelPlanner.Api.Contracts;
using SmartTravelPlanner.Api.Models.TravelPlanning;

public interface ITravelAgent
{
    Task<TripPlan> CreateItineraryAsync(
        TravelPlanRequest request,
        CancellationToken cancellationToken = default);
}
