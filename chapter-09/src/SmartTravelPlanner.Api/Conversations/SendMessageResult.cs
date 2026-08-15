using SmartTravelPlanner.Api.Models.Execution;

namespace SmartTravelPlanner.Api.Conversations;

public sealed record SendMessageResult(SendMessageOutcome Outcome, TripPlanResponse? Response = null);
