using Microsoft.Agents.AI.Workflows;
using SmartTravelPlanner.Api.Execution;
using SmartTravelPlanner.Api.Live;
using SmartTravelPlanner.Api.Persistence;
using SmartTravelPlanner.Api.Workflows.Messages;
using SmartTravelPlanner.Contracts;
using ApiExecutionTrace =
    SmartTravelPlanner.Api.Models.Execution.ExecutionTrace;
using ApiWorkflowExecutionTrace =
    SmartTravelPlanner.Api.Models.Execution.WorkflowExecutionTrace;
using TripPlanResponse =
    SmartTravelPlanner.Api.Models.Execution.TripPlanResponse;

namespace SmartTravelPlanner.Api.Workflows;

public sealed class TravelWorkflowService(
    TravelPlanningWorkflow workflowFactory,
    IExecutionTraceRecorder executionTraceRecorder,
    IWorkflowTraceRecorder workflowTraceRecorder,
    IWorkflowLiveEventPublisher liveEvents,
    TimeProvider timeProvider,
    IWorkflowRunStore runStore,
    ILogger<TravelWorkflowService> logger)
    : ITravelWorkflowService
{
    public async Task<TripPlanResponse> ExecuteAsync(
        TravelPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Guid runId = Guid.NewGuid();
        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        string originalRequest = OriginalRequest(request);

        await runStore.StartAsync(
            runId,
            request,
            originalRequest,
            startedAt,
            cancellationToken);

        return await ExecuteExistingRunAsync(
            runId,
            request,
            originalRequest,
            cancellationToken);
    }

    public async Task<TripPlanResponse> ExecuteExistingRunAsync(
        Guid runId,
        TravelPlanRequest request,
        string originalRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        DateTimeOffset startedAt = timeProvider.GetUtcNow();

        workflowTraceRecorder.WorkflowStarted(runId, startedAt);

        logger.LogInformation(
            "Workflow Started: {WorkflowRunId}",
            runId);

        await liveEvents.PublishAsync(
            runId,
            WorkflowLiveEventType.WorkflowStarted,
            WorkflowStageType.Workflow,
            "TravelPlanningWorkflow",
            WorkflowStageStatus.Running,
            summary: "Workflow started.",
            cancellationToken: cancellationToken);

        await PublishWaitingExecutorsAsync(runId, cancellationToken);

        using ExecutionTraceScope traceScope =
            executionTraceRecorder.BeginRequest(runId);

        try
        {
            TravelWorkflowRequest input = new(
                runId,
                request,
                originalRequest);

            Workflow workflow = workflowFactory.Create();

            Run run = await InProcessExecution.RunAsync(
                workflow,
                input,
                cancellationToken: cancellationToken);

            TripPlanResponse? response = ExtractResponse(run, runId);

            if (response is null)
            {
                throw new InvalidOperationException(
                    "The workflow completed without a TripPlanResponse.");
            }

            ApiWorkflowExecutionTrace workflowTrace =
                workflowTraceRecorder.WorkflowCompleted(
                    runId,
                    timeProvider.GetUtcNow(),
                    "Completed");

            await runStore.SaveDiagnosticsAsync(
                runId,
                response.Execution,
                response.TripPlan,
                cancellationToken);

            await runStore.CompleteAsync(
                runId,
                "Completed",
                workflowTrace.CompletedAt,
                ct: cancellationToken);

            await PublishDiagnosticsAsync(
                runId,
                response.Execution,
                cancellationToken);

            await liveEvents.PublishAsync(
                runId,
                WorkflowLiveEventType.WorkflowCompleted,
                WorkflowStageType.Workflow,
                "TravelPlanningWorkflow",
                WorkflowStageStatus.Completed,
                durationMs: workflowTrace.DurationMs,
                summary: "Workflow completed and produced a TripPlan.",
                data: new
                {
                    response.TripPlan.Destination,
                    response.TripPlan.DurationDays
                },
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Workflow Completed: {WorkflowRunId}",
                runId);

            return response with
            {
                Workflow = workflowTrace
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            DateTimeOffset cancelledAt = timeProvider.GetUtcNow();

            workflowTraceRecorder.WorkflowCompleted(
                runId,
                cancelledAt,
                "Cancelled");

            await PersistPartialSafelyAsync(runId);

            await runStore.CompleteAsync(
                runId,
                "Cancelled",
                cancelledAt,
                "Cancellation",
                "Request cancelled",
                CancellationToken.None);

            await liveEvents.PublishAsync(
                runId,
                WorkflowLiveEventType.WorkflowCancelled,
                WorkflowStageType.Workflow,
                "TravelPlanningWorkflow",
                WorkflowStageStatus.Cancelled,
                summary: "Workflow cancelled.",
                cancellationToken: CancellationToken.None);

            logger.LogWarning(
                "Workflow Cancelled: {WorkflowRunId}",
                runId);

            throw;
        }
        catch (Exception exception)
        {
            DateTimeOffset failedAt = timeProvider.GetUtcNow();

            ApiWorkflowExecutionTrace failedTrace =
                workflowTraceRecorder.WorkflowCompleted(
                    runId,
                    failedAt,
                    "Failed",
                    exception);

            await PersistPartialSafelyAsync(runId);

            await runStore.CompleteAsync(
                runId,
                "Failed",
                failedAt,
                failedTrace.Executors.LastOrDefault()?.ExecutorName,
                exception.Message,
                CancellationToken.None);

            await liveEvents.PublishAsync(
                runId,
                WorkflowLiveEventType.WorkflowFailed,
                WorkflowStageType.Workflow,
                "TravelPlanningWorkflow",
                WorkflowStageStatus.Failed,
                summary: exception.Message,
                cancellationToken: CancellationToken.None);

            logger.LogError(
                exception,
                "Workflow Failed: {WorkflowRunId}",
                runId);

            throw;
        }
    }

    public static string OriginalRequest(TravelPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return
            $"Plan a {request.DurationDays}-day trip to " +
            $"{request.Destination}. {request.Preferences}";
    }

    private TripPlanResponse? ExtractResponse(
        Run run,
        Guid runId)
    {
        TripPlanResponse? response = null;

        foreach (WorkflowEvent workflowEvent in run.NewEvents)
        {
            logger.LogDebug(
                "Workflow event {EventType} received for {WorkflowRunId}",
                workflowEvent.GetType().Name,
                runId);

            if (workflowEvent is WorkflowOutputEvent
                {
                    Data: TripPlanResponse result
                })
            {
                response = result;
            }
        }

        return response;
    }

    private async Task PublishWaitingExecutorsAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        foreach (WorkflowNodeDto node
                 in WorkflowTopologyProvider.TravelPlanning().Nodes)
        {
            await liveEvents.PublishAsync(
                runId,
                WorkflowLiveEventType.ExecutorWaiting,
                WorkflowStageType.Executor,
                node.Name,
                WorkflowStageStatus.Waiting,
                node.InputMessageType,
                node.OutputMessageType,
                summary: "Waiting for previous workflow stage.",
                cancellationToken: cancellationToken);
        }
    }

    private async Task PublishDiagnosticsAsync(
        Guid runId,
        ApiExecutionTrace trace,
        CancellationToken cancellationToken)
    {
        foreach (var tool in trace.ToolCalls.OrderBy(x => x.Order))
        {
            WorkflowLiveEventType eventType =
                tool.Status == "Success"
                    ? WorkflowLiveEventType.ToolCompleted
                    : WorkflowLiveEventType.ToolFailed;

            WorkflowStageStatus status =
                tool.Status == "Success"
                    ? WorkflowStageStatus.Completed
                    : WorkflowStageStatus.Failed;

            await liveEvents.PublishAsync(
                runId,
                eventType,
                WorkflowStageType.Tool,
                tool.ToolName,
                status,
                durationMs: tool.DurationMs,
                summary:
                    $"Step {tool.PlanStepOrder ?? tool.Order}: " +
                    $"{tool.Status}; retry count {tool.RetryCount}.",
                data: new
                {
                    tool.Order,
                    tool.PlanStepOrder,
                    tool.RetryCount
                },
                cancellationToken: cancellationToken);
        }

        if (trace.ExecutionPlan?.RepairAttempted == true)
        {
            await liveEvents.PublishAsync(
                runId,
                WorkflowLiveEventType.AgentCompleted,
                WorkflowStageType.Agent,
                "Repair Agent",
                WorkflowStageStatus.Completed,
                summary:
                    "Repair agent completed execution-plan repair.",
                cancellationToken: cancellationToken);
        }

        await liveEvents.PublishAsync(
            runId,
            WorkflowLiveEventType.AgentCompleted,
            WorkflowStageType.Agent,
            "Travel Agent",
            WorkflowStageStatus.Completed,
            summary: "Travel Agent produced the final itinerary.",
            cancellationToken: cancellationToken);
    }

    private async Task PersistPartialSafelyAsync(Guid runId)
    {
        try
        {
            ApiExecutionTrace? partialTrace =
                executionTraceRecorder.GetCurrentTrace();

            if (partialTrace is null)
            {
                return;
            }

            await runStore.SaveDiagnosticsAsync(
                runId,
                partialTrace,
                tripPlan: null,
                CancellationToken.None);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(
                exception,
                "No partial execution trace was available for workflow {WorkflowRunId}.",
                runId);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to persist partial diagnostics for workflow {WorkflowRunId}.",
                runId);
        }
    }
}
