using SmartTravelPlanner.Api.Models.TravelPlanning;

namespace SmartTravelPlanner.Api.Conversations;

public sealed record SendMessageResult(SendMessageOutcome Outcome, TripPlan? Plan = null);
