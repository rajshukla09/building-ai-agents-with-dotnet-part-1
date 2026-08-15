using Microsoft.Extensions.Logging.Abstractions;
using SmartTravelPlanner.Api.Classification;
using SmartTravelPlanner.Api.Routing;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class ToolRouterTests
{
    private readonly ToolRouter _router = new(NullLogger<ToolRouter>.Instance);

    [Theory]
    [InlineData(ToolType.Distance, "DistanceTool")]
    [InlineData(ToolType.Currency, "CurrencyTool")]
    [InlineData(ToolType.LocalTime, "TimeZoneTool")]
    [InlineData(ToolType.Weather, "WeatherTool")]
    public void Step_RoutesFromToolEnum(ToolType tool, string expectedTool)
    {
        ToolRouteDecision route = _router.Route(CreateValid(tool));
        Assert.True(route.IsMandatory);
        Assert.Equal(expectedTool, route.ToolName);
        Assert.Equal(1, route.StepOrder);
    }

    [Fact]
    public void Route_NormalizesCaseInsensitiveArguments()
    {
        ExecutionStep step = new()
        {
            Order = 1,
            Tool = ToolType.Distance,
            Arguments = new()
            {
                ["ORIGIN"] = "Hyderabad",
                ["Destination"] = "Jaipur"
            }
        };

        ToolRouteDecision route = _router.Route(step);

        Assert.Equal("Hyderabad", route.Arguments["origin"]);
        Assert.Equal("Jaipur", route.Arguments["destination"]);
    }

    private static ExecutionStep CreateValid(ToolType tool) => new()
    {
        Order = 1,
        Tool = tool,
        Arguments = tool switch
        {
            ToolType.Distance => new() { ["origin"] = "Hyderabad", ["destination"] = "Jaipur" },
            ToolType.Currency => new() { ["amount"] = 100m, ["from"] = "USD", ["to"] = "INR" },
            ToolType.LocalTime => new() { ["city"] = "Tokyo" },
            _ => new() { ["destination"] = "Tokyo" }
        }
    };
}
