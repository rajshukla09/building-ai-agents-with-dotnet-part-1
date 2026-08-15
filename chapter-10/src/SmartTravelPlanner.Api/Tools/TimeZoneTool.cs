using System.ComponentModel;

namespace SmartTravelPlanner.Api.Tools;

public sealed class TimeZoneTool(TimeProvider timeProvider)
{
    private static readonly IReadOnlyDictionary<string, TimeSpan> Offsets =
        new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tokyo"] = TimeSpan.FromHours(9),
            ["Jaipur"] = TimeSpan.FromMinutes(330),
            ["Hyderabad"] = TimeSpan.FromMinutes(330),
            ["London"] = TimeSpan.Zero,
            ["Riyadh"] = TimeSpan.FromHours(3)
        };

    [Description("Gets a city's local time from a fixed UTC offset. Use whenever the user asks for local time or a time-zone comparison.")]
    public LocalTimeResult GetLocalTime(
        [Description("City whose local time is needed.")] string city)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        string normalizedCity = city.Trim();
        if (!Offsets.TryGetValue(normalizedCity, out TimeSpan offset))
        {
            throw new ArgumentException(
                "Supported cities are Tokyo, Jaipur, Hyderabad, London, and Riyadh.",
                nameof(city));
        }

        DateTimeOffset localTime = timeProvider.GetUtcNow().ToOffset(offset);
        return new LocalTimeResult(
                    normalizedCity,
                    FormatOffset(offset),
                    localTime.ToString("yyyy-MM-dd'T'HH:mm"));
    }

    private static string FormatOffset(TimeSpan offset) =>
        $"{(offset < TimeSpan.Zero ? '-' : '+')}{offset.Duration():hh\\:mm}";
}

public sealed record LocalTimeResult(string City, string UtcOffset, string LocalTime);
