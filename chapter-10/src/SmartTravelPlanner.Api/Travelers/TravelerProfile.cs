namespace SmartTravelPlanner.Api.Travelers;

/// <summary>Durable traveller identity. Preferences and conversation state are stored separately.</summary>
public sealed record TravelerProfile
{
    public required Guid TravelerId
    {
        get; init;
    }
    public required DateTimeOffset CreatedAt
    {
        get; init;
    }
}
