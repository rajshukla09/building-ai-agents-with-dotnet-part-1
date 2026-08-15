using System.ComponentModel;
using SmartTravelPlanner.Api.Execution;

namespace SmartTravelPlanner.Api.Tools;

public sealed class DistanceTool(IExecutionTraceRecorder traceRecorder)
{
    private static readonly IReadOnlyDictionary<string, int> Distances =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [Key("Hyderabad", "Jaipur")] = 1560,
            [Key("Jaipur", "Delhi")] = 280,
            [Key("Tokyo", "Kyoto")] = 450,
            [Key("London", "Paris")] = 455
        };

    [Description("Gets a fixed sample road distance between two cities. Use for distance, route-length, or how-far questions.")]
    public DistanceResult GetDistance(
        [Description("Starting city.")] string origin,
        [Description("Destination city.")] string destination)
    {
        return traceRecorder.RecordToolCall(
            nameof(DistanceTool),
            new { origin, destination },
            () =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(origin);
                ArgumentException.ThrowIfNullOrWhiteSpace(destination);
                string normalizedOrigin = origin.Trim();
                string normalizedDestination = destination.Trim();
                if (!Distances.TryGetValue(Key(normalizedOrigin, normalizedDestination), out int distance))
                {
                    throw new ArgumentException("No sample distance is available for that route.");
                }

                return new DistanceResult(normalizedOrigin, normalizedDestination, distance);
            });
    }

    private static string Key(string first, string second) =>
        string.Compare(first, second, StringComparison.OrdinalIgnoreCase) <= 0
            ? $"{first}|{second}"
            : $"{second}|{first}";
}

public sealed record DistanceResult(string Origin, string Destination, int DistanceKm);
