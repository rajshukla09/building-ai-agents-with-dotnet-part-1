using System.Collections.Concurrent;
using SmartTravelPlanner.Api.Models.Execution;
using SmartTravelPlanner.Api.Persistence;

namespace SmartTravelPlanner.Api.Workflows;

public interface IWorkflowTraceRecorder
{
    void WorkflowStarted(Guid runId, DateTimeOffset startedAt);

    void ExecutorCompleted(ExecutorExecutionTrace execution);

    WorkflowExecutionTrace WorkflowCompleted(Guid runId, DateTimeOffset completedAt, string status, Exception? exception = null);
}

public sealed class WorkflowTraceRecorder : IWorkflowTraceRecorder
{
    public WorkflowTraceRecorder(TimeProvider timeProvider, IWorkflowRunStore store)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store;
    }

    public WorkflowTraceRecorder(TimeProvider system)
    {
        _system = system;
    }

    private readonly IWorkflowRunStore _store;

    private readonly ConcurrentDictionary<Guid, RunTrace> _runs = new();
    private TimeProvider _system;

    public void WorkflowStarted(Guid runId, DateTimeOffset startedAt) =>
        _runs[runId] = new RunTrace(startedAt);

    public void ExecutorCompleted(ExecutorExecutionTrace execution)
    {
        if (_runs.TryGetValue(execution.WorkflowRunId, out RunTrace? run))
            run.Executions.Enqueue(execution);
        try
        {
            _store.RecordExecutorAsync(execution).GetAwaiter().GetResult();
        }
        catch { /* Diagnostics must never alter workflow control flow. */ }
    }

    public WorkflowExecutionTrace WorkflowCompleted(Guid runId, DateTimeOffset completedAt, string status, Exception? exception = null)
    {
        if (!_runs.TryRemove(runId, out RunTrace? run))
            run = new RunTrace(completedAt);
        return new WorkflowExecutionTrace
        {
            WorkflowRunId = runId,
            StartedAt = run.StartedAt,
            CompletedAt = completedAt,
            DurationMs = Math.Max(0, (long)(completedAt - run.StartedAt).TotalMilliseconds),
            Status = status,
            Exception = exception?.Message,
            Executors = run.Executions.ToArray(),
            MessageTransitions = run.Executions.Select((execution, index) => new WorkflowMessageTransitionDto
            {
                Order = index + 1,
                ExecutorName = execution.ExecutorName,
                InputMessageType = execution.MessageType,
                OutputMessageType = execution.OutputMessageType ?? "Unknown",
                CarriedForward = new Dictionary<string, object?> { ["workflowRunId"] = execution.WorkflowRunId },
                Added = new Dictionary<string, object?> { ["stageCompleted"] = execution.Status },
                Changed = new Dictionary<string, WorkflowValueChangeDto>()
            }).ToArray()
        };
    }

    private sealed record RunTrace(DateTimeOffset StartedAt)
    {
        public ConcurrentQueue<ExecutorExecutionTrace> Executions { get; } = new();
    }
}
