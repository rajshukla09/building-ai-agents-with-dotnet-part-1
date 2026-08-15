using Microsoft.Extensions.Logging.Abstractions;
using SmartTravelPlanner.Api.Classification;
using SmartTravelPlanner.Api.Execution;
using SmartTravelPlanner.Api.Routing;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class ExecutionPlanExecutorTests
{
    [Fact]
    public async Task FourToolPlan_ExecutesInOrderAndIncludesEveryResult()
    {
        FakePipeline pipeline = new();
        ExecutionPlanExecutor executor = CreateExecutor(pipeline);
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

        string prompt = await executor.EnrichRequestAsync("Plan my trip", plan);

        Assert.Equal([1, 2, 3, 4], pipeline.Routes.Select(route => route.StepOrder));
        Assert.Contains("WeatherTool result", prompt);
        Assert.Contains("DistanceTool result", prompt);
        Assert.Contains("CurrencyTool result", prompt);
        Assert.Contains("TimeZoneTool result", prompt);
    }

    [Fact]
    public async Task PartialFailure_ContinuesAndIncludesFailureAndLaterResult()
    {
        FakePipeline pipeline = new(failStep: 2);
        ExecutionPlanExecutor executor = CreateExecutor(pipeline);
        ExecutionPlan plan = new()
        {
            Intent = RequestIntent.TravelPlanning,
            Steps =
            [
                Step(1, ToolType.Weather, ("destination", "Jaipur")),
                Step(2, ToolType.Distance, ("origin", "Hyderabad"), ("destination", "Jaipur")),
                Step(3, ToolType.LocalTime, ("city", "Jaipur"))
            ]
        };

        string prompt = await executor.EnrichRequestAsync("Plan my trip", plan);

        Assert.Equal([1, 2, 3], pipeline.Routes.Select(route => route.StepOrder));
        Assert.Contains("Failure", prompt);
        Assert.Contains("DistanceTool could not complete", prompt);
        Assert.Contains("TimeZoneTool result", prompt);
    }

    [Fact]
    public async Task NoToolPlan_ReturnsOriginalRequest()
    {
        FakePipeline pipeline = new();
        string prompt = await CreateExecutor(pipeline).EnrichRequestAsync(
            "Plan a Jaipur itinerary", new ExecutionPlan { Intent = RequestIntent.TravelPlanning });
        Assert.Equal("Plan a Jaipur itinerary", prompt);
        Assert.Empty(pipeline.Routes);
    }

    private static ExecutionPlanExecutor CreateExecutor(FakePipeline pipeline)
    {
        TimeProvider clock = TimeProvider.System;
        return new(new ToolRouter(NullLogger<ToolRouter>.Instance), pipeline,
            new ToolExecutionTraceRecorder(clock), clock, NullLogger<ExecutionPlanExecutor>.Instance);
    }

    private static ExecutionStep Step(int order, ToolType tool, params (string Key, object? Value)[] arguments) =>
        new() { Order = order, Tool = tool, Arguments = arguments.ToDictionary(item => item.Key, item => item.Value) };

    private sealed class FakePipeline(int? failStep = null) : IToolExecutionPipeline
    {
        public List<ToolRouteDecision> Routes { get; } = [];

        public Task<object?> ExecuteAsync(ToolRouteDecision decision, CancellationToken cancellationToken = default)
        {
            Routes.Add(decision);
            if (decision.StepOrder == failStep)
                throw new ToolExecutionFailedException(decision.ToolName!, "simulated failure");
            return Task.FromResult<object?>($"{decision.ToolName} result");
        }

        public T ExecuteModelSelected<T>(string toolName, object input, Func<T> operation) => operation();
    }
}
