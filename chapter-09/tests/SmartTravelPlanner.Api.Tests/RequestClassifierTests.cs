using SmartTravelPlanner.Api.Classification;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class RequestClassifierTests
{
    private readonly ExecutionPlanValidator _validator = new();
    [Fact]
    public void NoToolPlan_IsValid()
    {
        ExecutionPlan plan = new() { Intent = RequestIntent.TravelPlanning };
        Assert.True(_validator.Validate(plan).IsValid);
        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void SingleToolPlan_IsValid()
    {
        ExecutionPlan plan = new()
        {
            Intent = RequestIntent.WeatherLookup,
            Steps = [Step(1, ToolType.Weather, ("destination", "Jaipur"))]
        };
        Assert.True(_validator.Validate(plan).IsValid);
    }

    [Fact]
    public void FourToolPlan_IsValidAndPreservesOrder()
    {
        ExecutionPlan plan = new()
        {
            Intent = RequestIntent.TravelPlanning,
            Steps =
            [
                Step(1, ToolType.Weather, ("destination", "Jaipur")),
                Step(2, ToolType.Distance, ("origin", "Hyderabad"), ("destination", "Jaipur")),
                Step(3, ToolType.Currency, ("amount", 500m), ("from", "USD"), ("to", "INR")),
                Step(4, ToolType.LocalTime, ("city", "Jaipur"))
            ]
        };
        Assert.True(_validator.Validate(plan).IsValid);
        Assert.Equal([ToolType.Weather, ToolType.Distance, ToolType.Currency, ToolType.LocalTime],
            plan.Steps.Select(step => step.Tool));
    }

    [Theory]
    [MemberData(nameof(InvalidPlans))]
    public void InvalidPlan_ReturnsMeaningfulError(ExecutionPlan plan, string message)
    {
        ExecutionPlanValidationResult result = _validator.Validate(plan);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains(message, StringComparison.OrdinalIgnoreCase));
    }

    public static TheoryData<ExecutionPlan, string> InvalidPlans => new()
    {
        { new() { Intent = RequestIntent.DistanceLookup }, "Distance" },
        { new() { Intent = RequestIntent.DistanceLookup, Steps = [Step(1, ToolType.Distance, ("origin", "Jaipur"))] }, "destination" },
        { new() { Intent = RequestIntent.CurrencyConversion, Steps = [Step(1, ToolType.Currency, ("amount", 5), ("from", "USD"))] }, "to" },
        { new() { Intent = RequestIntent.LocalTime, Steps = [Step(1, ToolType.LocalTime)] }, "city" },
        { new() { Intent = RequestIntent.WeatherLookup, Steps = [Step(2, ToolType.Weather, ("destination", "Tokyo"))] }, "order" }
    };

    [Fact]
    public void ArgumentKeys_AreCaseInsensitive()
    {
        ExecutionPlan plan = new()
        {
            Intent = RequestIntent.TravelPlanning,
            Steps =
            [
                Step(1, ToolType.Weather, ("Destination", "Jaipur")),
                Step(2, ToolType.Distance, ("ORIGIN", "Hyderabad"), ("DESTINATION", "Jaipur")),
                Step(3, ToolType.Currency, ("Amount", 500m), ("FROM", "USD"), ("To", "INR")),
                Step(4, ToolType.LocalTime, ("CITY", "Jaipur"))
            ]
        };

        Assert.True(_validator.Validate(plan).IsValid);
        Assert.True(ExecutionPlanValidator.TryGetArgument(plan.Steps[0], "destination", out object? destination));
        Assert.Equal("Jaipur", destination);
    }

    private static ExecutionStep Step(int order, ToolType tool, params (string Key, object? Value)[] arguments) =>
        new() { Order = order, Tool = tool, Arguments = arguments.ToDictionary(item => item.Key, item => item.Value) };
}
