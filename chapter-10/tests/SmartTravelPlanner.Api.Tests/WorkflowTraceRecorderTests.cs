using SmartTravelPlanner.Api.Models.Execution;
using SmartTravelPlanner.Api.Workflows;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class WorkflowTraceRecorderTests
{
    [Fact]
    public void CompletionPreservesExecutorOrderAndLifecycleData()
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        Guid runId = Guid.NewGuid();
        WorkflowTraceRecorder recorder = new(TimeProvider.System);
        recorder.WorkflowStarted(runId, started);
        recorder.ExecutorCompleted(Execution(runId, "First", "TravelWorkflowRequest", started));
        recorder.ExecutorCompleted(Execution(runId, "Second", "ExecutionPlanMessage", started.AddMilliseconds(1)));

        WorkflowExecutionTrace trace = recorder.WorkflowCompleted(
            runId, started.AddMilliseconds(5), "Completed");

        Assert.Equal(runId, trace.WorkflowRunId);
        Assert.Equal("Completed", trace.Status);
        Assert.Equal(5, trace.DurationMs);
        Assert.Equal(["First", "Second"], trace.Executors.Select(item => item.ExecutorName));
        Assert.Equal(["TravelWorkflowRequest", "ExecutionPlanMessage"],
            trace.Executors.Select(item => item.MessageType));
    }

    [Fact]
    public void FailureCapturesExceptionWithoutLosingCompletedExecutors()
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        Guid runId = Guid.NewGuid();
        WorkflowTraceRecorder recorder = new(TimeProvider.System);
        recorder.WorkflowStarted(runId, started);
        recorder.ExecutorCompleted(Execution(runId, "Planning", "TravelWorkflowRequest", started));

        WorkflowExecutionTrace trace = recorder.WorkflowCompleted(
            runId, started.AddMilliseconds(2), "Failed", new InvalidOperationException("classification failed"));

        Assert.Equal("Failed", trace.Status);
        Assert.Equal("classification failed", trace.Exception);
        Assert.Single(trace.Executors);
    }

    private static ExecutorExecutionTrace Execution(
        Guid runId, string executor, string message, DateTimeOffset started) => new()
        {
            WorkflowRunId = runId,
            ExecutorName = executor,
            MessageType = message,
            StartedAt = started,
            CompletedAt = started.AddMilliseconds(1),
            DurationMs = 1,
            Status = "Completed"
        };
}
