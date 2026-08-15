using SmartTravelPlanner.Api.Classification;
using SmartTravelPlanner.Api.Execution;
using SmartTravelPlanner.Api.Live;
using SmartTravelPlanner.Api.Workflows.Messages;

namespace SmartTravelPlanner.Api.Workflows.Executors;

public sealed class ExecutionPlanExecutor(
    IExecutionPlanProvider provider,
    IExecutionTraceRecorder executionTraceRecorder,
    IWorkflowTraceRecorder traceRecorder,
    IWorkflowLiveEventPublisher liveEvents,
    TimeProvider timeProvider,
    ILogger<ExecutionPlanExecutor> logger)
    : WorkflowExecutor<TravelWorkflowRequest, ExecutionPlanMessage>(
        nameof(ExecutionPlanExecutor), traceRecorder, liveEvents, timeProvider, logger)
{
    protected override async ValueTask<ExecutionPlanMessage> ExecuteAsync(
        TravelWorkflowRequest message, CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        ExecutionPlan plan = await provider.CreateAsync(message.OriginalUserRequest, cancellationToken);
        executionTraceRecorder.RecordExecutionPlanClassification(startedAt, timeProvider.GetUtcNow(), plan);
        return new(message.WorkflowRunId, message.Request, message.OriginalUserRequest, plan);
    }
}
