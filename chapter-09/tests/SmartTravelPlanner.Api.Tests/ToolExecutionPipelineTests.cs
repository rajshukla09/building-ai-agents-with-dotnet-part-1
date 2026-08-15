using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartTravelPlanner.Api.Configuration;
using SmartTravelPlanner.Api.Classification;
using SmartTravelPlanner.Api.Execution;
using SmartTravelPlanner.Api.Tools;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class ToolExecutionPipelineTests
{
    [Fact]
    public async Task TransientFailure_RetriesThenRecordsSuccess()
    {
        (ToolExecutionPipeline pipeline, ToolExecutionTraceRecorder recorder) = CreatePipeline(maximumRetries: 3);
        int attempts = 0;
        using ExecutionTraceScope scope = recorder.BeginRequest();

        object? result = await pipeline.ExecuteWithPolicyAsync("TestTool", new { value = 1 },
            ToolInvocationMode.Deterministic, null, _ => ++attempts < 3
                ? throw new TransientToolException("temporary")
                : Task.FromResult<object?>("done"), CancellationToken.None);
        var trace = scope.Complete();

        Assert.Equal("done", result);
        Assert.Equal(3, attempts);
        Assert.Equal(2, Assert.Single(trace.ToolCalls).RetryCount);
    }

    [Fact]
    public async Task ExhaustedRetries_RecordFailureAndReason()
    {
        (ToolExecutionPipeline pipeline, ToolExecutionTraceRecorder recorder) = CreatePipeline(maximumRetries: 2);
        using ExecutionTraceScope scope = recorder.BeginRequest();
        await Assert.ThrowsAsync<ToolExecutionFailedException>(() => pipeline.ExecuteWithPolicyAsync(
            "TestTool", new { }, ToolInvocationMode.ModelSelected, null,
            _ => throw new TransientToolException("still unavailable"), CancellationToken.None));
        var call = Assert.Single(scope.Complete().ToolCalls);
        Assert.Equal("Failure", call.Status);
        Assert.Equal(2, call.RetryCount);
        Assert.Contains("still unavailable", call.FailureReason);
    }

    [Fact]
    public async Task Timeout_IsRecordedAndNotRetried()
    {
        (ToolExecutionPipeline pipeline, ToolExecutionTraceRecorder recorder) = CreatePipeline(timeoutSeconds: 1);
        using ExecutionTraceScope scope = recorder.BeginRequest();
        await Assert.ThrowsAsync<ToolExecutionFailedException>(() => pipeline.ExecuteWithPolicyAsync(
            "SlowTool", new { }, ToolInvocationMode.Deterministic, null,
            async token => { await Task.Delay(TimeSpan.FromSeconds(5), token); return null; }, CancellationToken.None));
        var call = Assert.Single(scope.Complete().ToolCalls);
        Assert.Equal("Timeout", call.Status);
        Assert.True(call.Timeout);
        Assert.Equal(0, call.RetryCount);
    }

    [Fact]
    public async Task Trace_PreservesExecutionOrderAndInvocationModes()
    {
        (ToolExecutionPipeline pipeline, ToolExecutionTraceRecorder recorder) = CreatePipeline();
        using ExecutionTraceScope scope = recorder.BeginRequest();
        await pipeline.ExecuteWithPolicyAsync("First", new { }, ToolInvocationMode.Deterministic, 1,
            _ => Task.FromResult<object?>(1), CancellationToken.None);
        await pipeline.ExecuteWithPolicyAsync("Second", new { }, ToolInvocationMode.ModelSelected, 2,
            _ => Task.FromResult<object?>(2), CancellationToken.None);
        var calls = scope.Complete().ToolCalls;
        Assert.Equal(new[] { 1, 2 }, calls.Select(x => x.Order));
        Assert.Equal(new[] { "Deterministic", "ModelSelected" }, calls.Select(x => x.InvocationMode));
        Assert.Equal(new int?[] { 1, 2 }, calls.Select(x => x.PlanStepOrder));
        Assert.All(calls, call => Assert.Equal("Success", call.Status));
    }

    [Fact]
    public void Trace_CapturesClassificationTelemetry()
    {
        TimeProvider clock = TimeProvider.System;
        ToolExecutionTraceRecorder recorder = new(clock);
        using ExecutionTraceScope scope = recorder.BeginRequest();
        DateTimeOffset startedAt = clock.GetUtcNow();
        var plan = new SmartTravelPlanner.Api.Classification.ExecutionPlan
        {
            Intent = SmartTravelPlanner.Api.Classification.RequestIntent.DistanceLookup,
            Steps = [new() { Order = 1, Tool = SmartTravelPlanner.Api.Classification.ToolType.Distance }]
        };
        ExecutionPlanValidationResult validation = new() { IsValid = true, Errors = [] };
        recorder.RecordExecutionPlanValidation(startedAt, clock.GetUtcNow(), plan, validation, false, null, validation);
        recorder.RecordPlanExecutionDuration(12);

        var executionPlan = Assert.IsType<SmartTravelPlanner.Api.Models.Execution.ExecutionPlanTrace>(
            scope.Complete().ExecutionPlan);
        Assert.Equal(plan, executionPlan.GeneratedPlan);
        Assert.True(executionPlan.InitialValidationSucceeded);
        Assert.False(executionPlan.RepairAttempted);
        Assert.True(executionPlan.FinalValidationSucceeded);
        Assert.Equal(12, executionPlan.PlanExecutionDurationMs);
    }

    private static (ToolExecutionPipeline, ToolExecutionTraceRecorder) CreatePipeline(
        int maximumRetries = 3, int timeoutSeconds = 5)
    {
        TimeProvider clock = TimeProvider.System;
        ToolExecutionTraceRecorder recorder = new(clock);
        ToolExecutionPipeline pipeline = new(recorder,
            Options.Create(new ToolExecutionOptions { MaximumRetries = maximumRetries, TimeoutSeconds = timeoutSeconds }),
            new WeatherTool(), new CurrencyTool(), new TimeZoneTool(clock), new DistanceTool(), clock,
            NullLogger<ToolExecutionPipeline>.Instance);
        return (pipeline, recorder);
    }
}
