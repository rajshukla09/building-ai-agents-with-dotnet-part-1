using Microsoft.Agents.AI.Workflows;
using SmartTravelPlanner.Api.Live;
using SmartTravelPlanner.Contracts;
using ApiExecutorExecutionTrace = SmartTravelPlanner.Api.Models.Execution.ExecutorExecutionTrace;

namespace SmartTravelPlanner.Api.Workflows.Executors;

public abstract class WorkflowExecutor<TInput, TOutput>(
    string name,
    IWorkflowTraceRecorder traceRecorder,
    IWorkflowLiveEventPublisher liveEvents,
    TimeProvider timeProvider,
    ILogger logger) : Executor<TInput, TOutput>(name)
{
    public override sealed async ValueTask<TOutput> HandleAsync(TInput message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        Guid runId = GetWorkflowRunId(message);
        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        logger.LogInformation("Executor Started: {ExecutorName} for workflow {WorkflowRunId}", name, runId);
        await liveEvents.PublishAsync(runId, WorkflowLiveEventType.ExecutorStarted, WorkflowStageType.Executor, name,
            WorkflowStageStatus.Running, typeof(TInput).Name, typeof(TOutput).Name, summary: $"{name} started.", cancellationToken: cancellationToken);
        try
        {
            TOutput output = await ExecuteAsync(message, cancellationToken);
            long durationMs = Record(runId, startedAt, "Completed", null);
            await liveEvents.PublishAsync(runId, WorkflowLiveEventType.ExecutorCompleted, WorkflowStageType.Executor, name,
                WorkflowStageStatus.Completed, typeof(TInput).Name, typeof(TOutput).Name, durationMs,
                $"{name} produced {typeof(TOutput).Name}.", cancellationToken: cancellationToken);
            await liveEvents.PublishAsync(runId, WorkflowLiveEventType.MessageProduced, WorkflowStageType.Message, typeof(TOutput).Name,
                WorkflowStageStatus.Completed, typeof(TInput).Name, typeof(TOutput).Name, durationMs,
                $"{typeof(TInput).Name} → {typeof(TOutput).Name}. Carried forward workflow context and added {typeof(TOutput).Name}.",
                new
                {
                    CarriedForward = new[] { "WorkflowRunId", "TravelPlanRequest", "OriginalUserRequest" },
                    Added = typeof(TOutput).Name,
                    Changed = Array.Empty<string>()
                }, cancellationToken);
            logger.LogInformation("Executor Completed: {ExecutorName} for workflow {WorkflowRunId}", name, runId);
            return output;
        }
        catch (Exception exception)
        {
            long durationMs = Record(runId, startedAt, exception is OperationCanceledException ? "Cancelled" : "Failed", exception);
            await liveEvents.PublishAsync(runId, WorkflowLiveEventType.ExecutorFailed, WorkflowStageType.Executor, name,
                exception is OperationCanceledException ? WorkflowStageStatus.Cancelled : WorkflowStageStatus.Failed,
                typeof(TInput).Name, typeof(TOutput).Name, durationMs, exception.Message, cancellationToken: cancellationToken);
            throw;
        }
    }

    protected abstract ValueTask<TOutput> ExecuteAsync(TInput message, CancellationToken cancellationToken);

    private long Record(Guid runId, DateTimeOffset startedAt, string status, Exception? exception)
    {
        DateTimeOffset completedAt = timeProvider.GetUtcNow();
        long durationMs = Math.Max(0, (long)(completedAt - startedAt).TotalMilliseconds);
        traceRecorder.ExecutorCompleted(new ApiExecutorExecutionTrace
        {
            WorkflowRunId = runId,
            ExecutorName = name,
            MessageType = typeof(TInput).Name,
            OutputMessageType = typeof(TOutput).Name,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            DurationMs = durationMs,
            Status = status,
            Exception = exception?.Message
        });
        return durationMs;
    }

    private static Guid GetWorkflowRunId(TInput message) =>
        (Guid)(typeof(TInput).GetProperty("WorkflowRunId")?.GetValue(message)
            ?? throw new InvalidOperationException($"{typeof(TInput).Name} must expose WorkflowRunId."));
}
