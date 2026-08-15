using SmartTravelPlanner.Api.Classification;
using SmartTravelPlanner.Api.Execution;
using SmartTravelPlanner.Api.Live;
using SmartTravelPlanner.Api.Routing;
using SmartTravelPlanner.Api.Workflows.Messages;

namespace SmartTravelPlanner.Api.Workflows.Executors;

public sealed class ToolExecutionExecutor(
    IToolRouter router,
    IToolExecutionPipeline pipeline,
    IExecutionTraceRecorder toolTraceRecorder,
    IWorkflowTraceRecorder workflowTraceRecorder,
    IWorkflowLiveEventPublisher liveEvents,
    TimeProvider timeProvider,
    ILogger<ToolExecutionExecutor> logger)
    : WorkflowExecutor<ValidatedPlanMessage, ToolExecutionMessage>(
        nameof(ToolExecutionExecutor), workflowTraceRecorder, liveEvents, timeProvider, logger)
{
    protected override async ValueTask<ToolExecutionMessage> ExecuteAsync(ValidatedPlanMessage message,
                                                                          CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        List<ToolStepResult> results = [];
        try
        {
            foreach (ExecutionStep step in message.ExecutionPlan.Steps.OrderBy(step => step.Order))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ToolRouteDecision route = router.Route(step);
                await liveEvents.PublishAsync(
                    message.WorkflowRunId, SmartTravelPlanner.Contracts.WorkflowLiveEventType.ToolStarted,
                    SmartTravelPlanner.Contracts.WorkflowStageType.Tool, step.Tool + "Tool",
                    SmartTravelPlanner.Contracts.WorkflowStageStatus.Running,
                    summary: $"Plan step {step.Order} started.", data: new { step.Order, step.Arguments },
                    cancellationToken: cancellationToken);
                try
                {
                    object? output = await pipeline.ExecuteAsync(route, cancellationToken);
                    results.Add(new(step.Order, step.Tool, "Success", output, null));
                    await liveEvents.PublishAsync(
                        message.WorkflowRunId, SmartTravelPlanner.Contracts.WorkflowLiveEventType.ToolCompleted,
                        SmartTravelPlanner.Contracts.WorkflowStageType.Tool, step.Tool + "Tool",
                        SmartTravelPlanner.Contracts.WorkflowStageStatus.Completed,
                        summary: $"Plan step {step.Order} completed.", data: new { step.Order, Output = output },
                        cancellationToken: cancellationToken);
                }
                catch (ToolExecutionFailedException exception)
                {
                    logger.LogWarning(exception, "Plan step {Order} ({Tool}) failed; continuing", step.Order,
                                      step.Tool);
                    results.Add(new(step.Order, step.Tool, "Failure", null, exception.Message));
                    await liveEvents.PublishAsync(
                        message.WorkflowRunId, SmartTravelPlanner.Contracts.WorkflowLiveEventType.ToolFailed,
                        SmartTravelPlanner.Contracts.WorkflowStageType.Tool, step.Tool + "Tool",
                        SmartTravelPlanner.Contracts.WorkflowStageStatus.Failed, summary: exception.Message,
                        data: new { step.Order }, cancellationToken: cancellationToken);
                }
            }
        }
        finally
        {
            toolTraceRecorder.RecordToolExecutionDuration(
                Math.Max(0, (long)(timeProvider.GetUtcNow() - startedAt).TotalMilliseconds));
        }

        return new(message.WorkflowRunId, message.Request, message.OriginalUserRequest, message.ExecutionPlan,
                   message.FinalValidation, message.Repaired, results);
    }
}
