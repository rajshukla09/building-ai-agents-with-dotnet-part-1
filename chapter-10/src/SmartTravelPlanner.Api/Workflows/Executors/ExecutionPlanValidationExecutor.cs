using SmartTravelPlanner.Api.Classification;
using SmartTravelPlanner.Api.Execution;
using SmartTravelPlanner.Api.Live;
using SmartTravelPlanner.Api.Workflows.Messages;

namespace SmartTravelPlanner.Api.Workflows.Executors;

public sealed class ExecutionPlanValidationExecutor(
    ExecutionPlanValidator validator,
    IExecutionPlanRepairAgent repairAgent,
    IExecutionTraceRecorder toolTraceRecorder,
    IWorkflowTraceRecorder workflowTraceRecorder,
    IWorkflowLiveEventPublisher liveEvents,
    TimeProvider timeProvider,
    ILogger<ExecutionPlanValidationExecutor> logger)
    : WorkflowExecutor<ExecutionPlanMessage, ValidatedPlanMessage>(
        nameof(ExecutionPlanValidationExecutor), workflowTraceRecorder, liveEvents, timeProvider, logger)
{
    protected override async ValueTask<ValidatedPlanMessage> ExecuteAsync(ExecutionPlanMessage message,
                                                                          CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        ExecutionPlanValidationResult initial = validator.Validate(message.ExecutionPlan);
        ExecutionPlan finalPlan = message.ExecutionPlan;
        ExecutionPlanValidationResult final = initial;
        bool repaired = false;
        DateTimeOffset? repairStartedAt = null;
        DateTimeOffset? repairCompletedAt = null;

        if (!initial.IsValid)
        {
            repaired = true;
            repairStartedAt = timeProvider.GetUtcNow();
            await liveEvents.PublishAsync(message.WorkflowRunId,
                                          SmartTravelPlanner.Contracts.WorkflowLiveEventType.AgentStarted,
                                          SmartTravelPlanner.Contracts.WorkflowStageType.Agent, "Repair Agent",
                                          SmartTravelPlanner.Contracts.WorkflowStageStatus.Running,
                                          summary: "Repair agent started.", cancellationToken: cancellationToken);
            logger.LogWarning("Execution plan invalid; attempting one repair: {Errors}", initial.Errors);
            try
            {
                finalPlan = await repairAgent.RepairAsync(message.OriginalUserRequest, message.ExecutionPlan,
                                                          initial.Errors, cancellationToken);
                repairCompletedAt = timeProvider.GetUtcNow();
                await liveEvents.PublishAsync(
                    message.WorkflowRunId, SmartTravelPlanner.Contracts.WorkflowLiveEventType.AgentCompleted,
                    SmartTravelPlanner.Contracts.WorkflowStageType.Agent, "Repair Agent",
                    SmartTravelPlanner.Contracts.WorkflowStageStatus.Completed,
                    durationMs: Math.Max(0, (long)(repairCompletedAt.Value - repairStartedAt!.Value).TotalMilliseconds),
                    summary: "Repair agent completed.", cancellationToken: cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                repairCompletedAt = timeProvider.GetUtcNow();
                ExecutionPlanValidationResult failedRepair =
                    new() { IsValid = false, Errors = [$"Repair agent failed: {exception.Message}"] };
                toolTraceRecorder.RecordExecutionPlanValidation(startedAt, repairCompletedAt.Value, initial, true,
                                                                repairStartedAt, repairCompletedAt, null, failedRepair);
                throw new RequestClassificationException(
                    $"The execution plan could not be repaired: {exception.Message}");
            }
            final = validator.Validate(finalPlan);
        }

        toolTraceRecorder.RecordExecutionPlanValidation(startedAt, timeProvider.GetUtcNow(), initial, repaired,
                                                        repairStartedAt, repairCompletedAt, repaired ? finalPlan : null,
                                                        final);
        if (!final.IsValid)
        {
            throw new RequestClassificationException(
                $"The execution plan remained invalid after one repair attempt: {string.Join("; ", final.Errors)}");
        }

        return new(message.WorkflowRunId, message.Request, message.OriginalUserRequest, finalPlan, initial, final,
                   repaired);
    }
}
