namespace SmartTravelPlanner.Api.Models.TravelPlanning;

public sealed record TripActivity
{
    public required string Time { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Category { get; init; }

    public required string Notes { get; init; }
}
