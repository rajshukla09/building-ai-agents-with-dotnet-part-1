namespace SmartTravelPlanner.Api.Models.TravelPlanning;

public sealed record TripPlan
{
    public required string Destination { get; init; }

    public required int DurationDays { get; init; }

    public required string Summary { get; init; }

    public IReadOnlyList<TripDay> Days { get; init; } = [];
}
