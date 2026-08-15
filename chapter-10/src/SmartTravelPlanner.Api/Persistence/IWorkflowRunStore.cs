using SmartTravelPlanner.Contracts;
using SmartTravelPlanner.Api.Agents.Results;
using ApiExecutionTrace = SmartTravelPlanner.Api.Models.Execution.ExecutionTrace;
using ApiExecutorExecutionTrace = SmartTravelPlanner.Api.Models.Execution.ExecutorExecutionTrace;

namespace SmartTravelPlanner.Api.Persistence;

public interface IWorkflowRunStore
{
    Task StartAsync(Guid runId, TravelPlanRequest request, string originalRequest, DateTimeOffset startedAt,
                    CancellationToken ct = default);

    Task RecordExecutorAsync(ApiExecutorExecutionTrace trace, CancellationToken ct = default);

    Task RecordAgentExecutionAsync(Guid runId, string executorName, AgentFailure? failure,
                                   AgentExecutionMetadata metadata, CancellationToken ct = default);

    Task SaveDiagnosticsAsync(Guid runId, ApiExecutionTrace trace, object? tripPlan, CancellationToken ct = default);

    Task CompleteAsync(Guid runId, string status, DateTimeOffset completedAt, string? failureStage = null,
                       string? error = null, CancellationToken ct = default);

    Task RecordLiveEventAsync(WorkflowLiveEventDto liveEvent, string? safeDataJson, CancellationToken ct = default);

    Task<IReadOnlyList<WorkflowLiveEventDto>> GetLiveEventsAsync(Guid workflowRunId, long afterSequence = 0,
                                                                 CancellationToken ct = default);

    Task<PagedWorkflowRunsDto> ListAsync(WorkflowRunQuery query, CancellationToken ct = default);
    Task<WorkflowRunDetailsDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowRunComparisonDto?> CompareAsync(Guid a, Guid b, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
