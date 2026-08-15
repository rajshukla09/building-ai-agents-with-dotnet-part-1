using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Agents.Results;
using SmartTravelPlanner.Api.Execution;
using SmartTravelPlanner.Api.Live;
using SmartTravelPlanner.Api.Models.Execution;
using SmartTravelPlanner.Api.Persistence;
using SmartTravelPlanner.Api.Models.TravelPlanning;
using SmartTravelPlanner.Api.Workflows.Messages;

namespace SmartTravelPlanner.Api.Workflows.Executors;

public sealed class TravelAgentExecutor(
    ITravelAgent travelAgent,
    IExecutionTraceRecorder toolTraceRecorder,
    IWorkflowTraceRecorder workflowTraceRecorder,
    IWorkflowLiveEventPublisher liveEvents,
    IWorkflowRunStore runStore,
    TimeProvider timeProvider,
    ILogger<TravelAgentExecutor> logger)
    : WorkflowExecutor<ToolExecutionMessage, TripPlanResponse>(
        nameof(TravelAgentExecutor), workflowTraceRecorder, liveEvents, timeProvider, logger)
{
    protected override async ValueTask<TripPlanResponse> ExecuteAsync(
        ToolExecutionMessage message, CancellationToken cancellationToken)
    {
        TravelAgentRequest request = new(
            message.OriginalUserRequest,
            message.Request,
            message.ToolResults,
            RuntimeContext: null,
            TravelerContext: null);

        await liveEvents.PublishAsync(message.WorkflowRunId, SmartTravelPlanner.Contracts.WorkflowLiveEventType.AgentStarted,
            SmartTravelPlanner.Contracts.WorkflowStageType.Agent, "Travel Agent",
            SmartTravelPlanner.Contracts.WorkflowStageStatus.Running,
            summary: "Travel Agent started generating the itinerary.", cancellationToken: cancellationToken);

        AgentResult<TripPlan> result = await travelAgent.ExecuteAsync(request, cancellationToken);

        if (!result.IsSuccess)
        {
            await runStore.RecordAgentExecutionAsync(message.WorkflowRunId, nameof(TravelAgentExecutor), result.Failure, result.Metadata, cancellationToken);
            await liveEvents.PublishAsync(message.WorkflowRunId, SmartTravelPlanner.Contracts.WorkflowLiveEventType.AgentFailed,
                SmartTravelPlanner.Contracts.WorkflowStageType.Agent, "Travel Agent",
                SmartTravelPlanner.Contracts.WorkflowStageStatus.Failed,
                durationMs: result.Metadata.DurationMs,
                summary: result.Failure!.Message,
                data: new
                {
                    result.Failure.Kind,
                    result.Failure.Code,
                    result.Failure.Path,
                    result.Failure.Retryable,
                    result.Metadata.AttemptCount,
                    result.Metadata.Status,
                    result.Metadata.Warnings
                },
                cancellationToken: cancellationToken);

            throw new AgentExecutionException(result.Failure!, result.Metadata);
        }

        await runStore.RecordAgentExecutionAsync(message.WorkflowRunId, nameof(TravelAgentExecutor), null, result.Metadata, cancellationToken);

        await liveEvents.PublishAsync(message.WorkflowRunId, SmartTravelPlanner.Contracts.WorkflowLiveEventType.AgentCompleted,
            SmartTravelPlanner.Contracts.WorkflowStageType.Agent, "Travel Agent",
            SmartTravelPlanner.Contracts.WorkflowStageStatus.Completed,
            durationMs: result.Metadata.DurationMs,
            summary: "Travel Agent completed the itinerary.",
            data: new
            {
                result.Metadata.AttemptCount,
                result.Metadata.Status,
                result.Metadata.StructuredDeserializationSucceeded,
                result.Metadata.RawRecoveryAttempted,
                result.Metadata.RawRecoverySucceeded,
                result.Metadata.RegenerationAttempted,
                result.Metadata.RegenerationSucceeded,
                result.Metadata.Warnings
            },
            cancellationToken: cancellationToken);

        return new TripPlanResponse(result.Value!, toolTraceRecorder.CaptureCurrent());
    }
}
