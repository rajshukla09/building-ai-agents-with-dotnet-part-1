using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Tools;
using SmartTravelPlanner.Api.Execution;
using SmartTravelPlanner.Api.Models.Execution;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class TravelToolsTests
{
    [Fact]
    public void WeatherTool_ReturnsJaipurSample()
    {
        WeatherResult result = new WeatherTool(CreateRecorder()).GetWeather("jaipur");

        Assert.Equal(31, result.Temperature);
        Assert.Equal("Sunny", result.Condition);
        Assert.Equal("Carry sunscreen and water.", result.Recommendation);
    }

    [Theory]
    [InlineData("USD", "INR", 100, 8350)]
    [InlineData("USD", "EUR", 100, 92)]
    [InlineData("SAR", "INR", 100, 2226.67)]
    public void CurrencyTool_UsesStaticCrossRates(
        string from,
        string to,
        decimal amount,
        decimal expected)
    {
        CurrencyConversionResult result = new CurrencyTool(CreateRecorder()).ConvertCurrency(from, to, amount);

        Assert.Equal(expected, result.ConvertedAmount);
    }

    [Fact]
    public void TimeZoneTool_UsesInjectedClockAndFixedOffset()
    {
        FixedTimeProvider clock = new(new DateTimeOffset(2026, 8, 3, 8, 30, 0, TimeSpan.Zero));
        TimeZoneTool tool = new(clock, new ToolExecutionTraceRecorder(clock));

        LocalTimeResult result = tool.GetLocalTime("Tokyo");

        Assert.Equal("Tokyo", result.City);
        Assert.Equal("+09:00", result.UtcOffset);
        Assert.Equal("2026-08-03T17:30", result.LocalTime);
    }

    [Fact]
    public void DistanceTool_ReturnsSameDistanceInEitherDirection()
    {
        DistanceTool tool = new(CreateRecorder());

        Assert.Equal(1560, tool.GetDistance("Hyderabad", "Jaipur").DistanceKm);
        Assert.Equal(1560, tool.GetDistance("Jaipur", "Hyderabad").DistanceKm);
    }

    [Fact]
    public void Instructions_MapScenariosToCorrectToolsAndAllowMultipleCalls()
    {
        string instructions = TravelAgentInstructions.SystemPrompt;

        Assert.Contains("GetWeather before planning", instructions);
        Assert.Contains("ConvertCurrency for every currency conversion", instructions);
        Assert.Contains("GetLocalTime whenever local time", instructions);
        Assert.Contains("GetDistance whenever", instructions);
        Assert.Contains("call each relevant tool", instructions);
        Assert.Contains("still returning a complete TripPlan", instructions);
        Assert.Contains("Preserve conversation context", instructions);
    }

    [Fact]
    public void MultipleTools_CanExecuteInOneConversationTurn()
    {
        IExecutionTraceRecorder recorder = CreateRecorder();
        WeatherResult weather = new WeatherTool(recorder).GetWeather("Jaipur");
        CurrencyConversionResult budget = new CurrencyTool(recorder).ConvertCurrency("USD", "INR", 100);
        DistanceResult distance = new DistanceTool(recorder).GetDistance("Hyderabad", "Jaipur");

        Assert.Equal("Sunny", weather.Condition);
        Assert.Equal(8350m, budget.ConvertedAmount);
        Assert.Equal(1560, distance.DistanceKm);
    }

    [Fact]
    public void Recorder_CapturesOneSuccessfulToolCallWithInputOutputAndDuration()
    {
        AdvancingTimeProvider clock = new();
        ToolExecutionTraceRecorder recorder = new(clock);
        WeatherTool tool = new(recorder);

        using ExecutionTraceScope scope = recorder.BeginRequest();
        WeatherResult result = tool.GetWeather("Jaipur");
        ExecutionTrace trace = scope.Complete();

        ToolExecution call = Assert.Single(trace.ToolCalls);
        Assert.Equal(1, call.Order);
        Assert.Equal(nameof(WeatherTool), call.ToolName);
        Assert.Equal("Success", call.Status);
        Assert.Equal(result, call.Output);
        Assert.NotNull(call.Input);
        Assert.True(call.DurationMs > 0);
        Assert.True(trace.TotalDurationMs > 0);
        Assert.True(trace.CompletedAt > trace.StartedAt);
    }

    [Fact]
    public void Recorder_PreservesMultipleToolInvocationOrder()
    {
        AdvancingTimeProvider clock = new();
        ToolExecutionTraceRecorder recorder = new(clock);

        using ExecutionTraceScope scope = recorder.BeginRequest();
        new DistanceTool(recorder).GetDistance("Hyderabad", "Jaipur");
        new CurrencyTool(recorder).ConvertCurrency("USD", "INR", 100);
        ExecutionTrace trace = scope.Complete();

        Assert.Equal(new[] { 1, 2 }, trace.ToolCalls.Select(call => call.Order));
        Assert.Equal(
            new[] { nameof(DistanceTool), nameof(CurrencyTool) },
            trace.ToolCalls.Select(call => call.ToolName));
    }

    [Fact]
    public void Recorder_RecordsFailedToolCallAndRethrows()
    {
        AdvancingTimeProvider clock = new();
        ToolExecutionTraceRecorder recorder = new(clock);
        DistanceTool tool = new(recorder);

        using ExecutionTraceScope scope = recorder.BeginRequest();
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => tool.GetDistance("Unknown", "Nowhere"));
        ExecutionTrace trace = scope.Complete();

        ToolExecution call = Assert.Single(trace.ToolCalls);
        Assert.Equal("Failure", call.Status);
        Assert.Null(call.Output);
        Assert.Equal(exception.Message, call.Error);
        Assert.True(call.DurationMs > 0);
    }

    [Fact]
    public void Recorder_NoToolRequestHasEmptyCollectionAndTotalDuration()
    {
        AdvancingTimeProvider clock = new();
        ToolExecutionTraceRecorder recorder = new(clock);

        using ExecutionTraceScope scope = recorder.BeginRequest();
        ExecutionTrace trace = scope.Complete();

        Assert.Empty(trace.ToolCalls);
        Assert.True(trace.TotalDurationMs > 0);
    }

    private static IExecutionTraceRecorder CreateRecorder()
    {
        TimeProvider clock = new FixedTimeProvider(DateTimeOffset.UnixEpoch);
        return new ToolExecutionTraceRecorder(clock);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class AdvancingTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 8, 3, 8, 30, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
        {
            DateTimeOffset current = _utcNow;
            _utcNow = _utcNow.AddMilliseconds(5);
            return current;
        }
    }
}
