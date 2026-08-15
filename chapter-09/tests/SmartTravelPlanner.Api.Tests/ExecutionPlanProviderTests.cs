using Microsoft.Extensions.Logging.Abstractions;
using SmartTravelPlanner.Api.Classification;
using SmartTravelPlanner.Api.Execution;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class ExecutionPlanProviderTests
{
    [Fact]
    public async Task InvalidInitialPlan_IsRepairedOnceAndRevalidated()
    {
        ExecutionPlan invalid = CurrencyPlan(new() { ["amount"] = 500m, ["from"] = "USD" });
        ExecutionPlan repaired = CurrencyPlan(new() { ["amount"] = 500m, ["from"] = "USD", ["to"] = "INR" });
        FakeRepairAgent repair = new(repaired);
        (ExecutionPlanProvider provider, ToolExecutionTraceRecorder recorder) = CreateProvider(invalid, repair);
        using ExecutionTraceScope scope = recorder.BeginRequest();

        ExecutionPlan result = await provider.CreateAsync("Convert 500 USD to INR");

        Assert.Same(repaired, result);
        Assert.Equal(1, repair.Attempts);
        var trace = Assert.IsType<SmartTravelPlanner.Api.Models.Execution.ExecutionPlanTrace>(scope.Complete().ExecutionPlan);
        Assert.False(trace.InitialValidationSucceeded);
        Assert.True(trace.RepairAttempted);
        Assert.True(trace.FinalValidationSucceeded);
        Assert.Same(repaired, trace.RepairedPlan);
    }

    [Fact]
    public async Task InvalidRepair_ThrowsAfterExactlyOneAttempt()
    {
        ExecutionPlan invalid = CurrencyPlan(new() { ["amount"] = 500m });
        FakeRepairAgent repair = new(invalid);
        (ExecutionPlanProvider provider, ToolExecutionTraceRecorder recorder) = CreateProvider(invalid, repair);
        using ExecutionTraceScope scope = recorder.BeginRequest();

        RequestClassificationException exception = await Assert.ThrowsAsync<RequestClassificationException>(
            () => provider.CreateAsync("Convert 500 USD to INR"));

        Assert.Contains("after one repair attempt", exception.Message);
        Assert.Equal(1, repair.Attempts);
        var trace = Assert.IsType<SmartTravelPlanner.Api.Models.Execution.ExecutionPlanTrace>(scope.Complete().ExecutionPlan);
        Assert.True(trace.RepairAttempted);
        Assert.False(trace.FinalValidationSucceeded);
        Assert.NotEmpty(trace.FinalValidationErrors);
    }

    [Fact]
    public async Task CaseInsensitiveKeys_PassWithoutRepair()
    {
        ExecutionPlan plan = CurrencyPlan(new() { ["Amount"] = 500m, ["FROM"] = "USD", ["To"] = "INR" });
        FakeRepairAgent repair = new(plan);
        (ExecutionPlanProvider provider, _) = CreateProvider(plan, repair);

        ExecutionPlan result = await provider.CreateAsync("Convert 500 USD to INR");

        Assert.Same(plan, result);
        Assert.Equal(0, repair.Attempts);
    }

    private static (ExecutionPlanProvider, ToolExecutionTraceRecorder) CreateProvider(
        ExecutionPlan classifiedPlan, FakeRepairAgent repair)
    {
        TimeProvider clock = TimeProvider.System;
        ToolExecutionTraceRecorder recorder = new(clock);
        ExecutionPlanProvider provider = new(new FakeClassifier(classifiedPlan), new ExecutionPlanValidator(),
            repair, recorder, clock, NullLogger<ExecutionPlanProvider>.Instance);
        return (provider, recorder);
    }

    private static ExecutionPlan CurrencyPlan(Dictionary<string, object?> arguments) => new()
    {
        Intent = RequestIntent.CurrencyConversion,
        Steps = [new ExecutionStep { Order = 1, Tool = ToolType.Currency, Arguments = arguments }]
    };

    private sealed class FakeClassifier(ExecutionPlan plan) : IRequestClassifier
    {
        public Task<ExecutionPlan> ClassifyAsync(string request, CancellationToken cancellationToken = default) =>
            Task.FromResult(plan);
    }

    private sealed class FakeRepairAgent(ExecutionPlan repairedPlan) : IExecutionPlanRepairAgent
    {
        public int Attempts { get; private set; }

        public Task<ExecutionPlan> RepairAsync(string originalRequest, ExecutionPlan invalidPlan,
            IReadOnlyList<string> validationErrors, CancellationToken cancellationToken = default)
        {
            Attempts++;
            return Task.FromResult(repairedPlan);
        }
    }
}
