namespace SmartTravelPlanner.Api.Agents;

public interface ITravelAgent
{
    Task<string> CreateItineraryAsync(string prompt, CancellationToken cancellationToken);
}
