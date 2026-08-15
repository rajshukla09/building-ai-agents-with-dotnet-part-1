using System.ComponentModel;

namespace SmartTravelPlanner.Api.Tools;

public sealed class WeatherTool
{
    private static readonly IReadOnlyDictionary<string, WeatherResult> Weather =
        new Dictionary<string, WeatherResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["Jaipur"] = new(31, "Sunny", "Carry sunscreen and water."),
            ["Tokyo"] = new(24, "Partly cloudy", "Pack a light layer and a compact umbrella."),
            ["Hyderabad"] = new(29, "Warm", "Stay hydrated during outdoor activities."),
            ["London"] = new(16, "Light rain", "Carry a waterproof jacket.")
        };

    [Description("Gets sample weather and practical travel advice for a destination. Use when planning a trip or when the user asks about weather.")]
    public WeatherResult GetWeather(
        [Description("City or destination whose weather is needed.")] string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        return Weather.TryGetValue(destination.Trim(), out WeatherResult? result)
            ? result
            : new WeatherResult(25, "Variable", "Check local conditions before departure.");
    }
}

public sealed record WeatherResult(int Temperature, string Condition, string Recommendation);
