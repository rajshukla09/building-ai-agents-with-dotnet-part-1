namespace SmartTravelPlanner.Api.Models.TravelPlanning;

public sealed record TripDay
{
    public required int DayNumber { get; init; }

    public required string Title { get; init; }

    public required List<TripActivity> Activities { get; init; }
}
