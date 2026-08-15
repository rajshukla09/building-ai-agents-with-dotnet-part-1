using SmartTravelPlanner.Api.Tools;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class TravelToolsTests
{
    [Fact]
    public void WeatherTool_ReturnsJaipurSample() =>
        Assert.Equal("Sunny", new WeatherTool().GetWeather("jaipur").Condition);

    [Theory]
    [InlineData("USD", "INR", 100, 8350)]
    [InlineData("USD", "EUR", 100, 92)]
    [InlineData("SAR", "INR", 100, 2226.67)]
    public void CurrencyTool_UsesStaticCrossRates(string from, string to, decimal amount, decimal expected) =>
        Assert.Equal(expected, new CurrencyTool().ConvertCurrency(from, to, amount).ConvertedAmount);

    [Fact]
    public void TimeZoneTool_UsesInjectedClockAndFixedOffset()
    {
        TimeZoneTool tool = new(new FixedTimeProvider(new DateTimeOffset(2026, 8, 3, 8, 30, 0, TimeSpan.Zero)));
        LocalTimeResult result = tool.GetLocalTime("Tokyo");
        Assert.Equal("+09:00", result.UtcOffset);
        Assert.Equal("2026-08-03T17:30", result.LocalTime);
    }

    [Fact]
    public void DistanceTool_ReturnsSameDistanceInEitherDirection()
    {
        DistanceTool tool = new();
        Assert.Equal(1560, tool.GetDistance("Hyderabad", "Jaipur").DistanceKm);
        Assert.Equal(1560, tool.GetDistance("Jaipur", "Hyderabad").DistanceKm);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
