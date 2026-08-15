using Microsoft.AspNetCore.SignalR;
using SmartTravelPlanner.Api.Agents.Results;
using SmartTravelPlanner.Api.Hubs;
using SmartTravelPlanner.Api.Live;
using SmartTravelPlanner.Api.Persistence;
using SmartTravelPlanner.Contracts;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class WorkflowLiveEventTests
{
    [Fact]
    public async Task PublishedEvents_UseIncreasingSequences_AndCanBeReplayed()
    {
        var store = new InMemoryLiveEventStore();
        var publisher = new SignalRWorkflowLiveEventPublisher(new NoopHubContext(), store, TimeProvider.System);
        Guid runId = Guid.NewGuid();

        await publisher.PublishAsync(runId, WorkflowLiveEventType.WorkflowStarted, WorkflowStageType.Workflow,
                                     "TravelPlanningWorkflow", WorkflowStageStatus.Running);
        await publisher.PublishAsync(runId, WorkflowLiveEventType.ExecutorStarted, WorkflowStageType.Executor,
                                     "ExecutionPlanExecutor", WorkflowStageStatus.Running);
        await publisher.PublishAsync(runId, WorkflowLiveEventType.ExecutorCompleted, WorkflowStageType.Executor,
                                     "ExecutionPlanExecutor", WorkflowStageStatus.Completed);

        IReadOnlyList<WorkflowLiveEventDto> events = await store.GetLiveEventsAsync(runId);
        Assert.Equal([1, 2, 3], events.Select(e => e.Sequence));
        Assert.Contains(events, e => e.EventType == WorkflowLiveEventType.ExecutorStarted);
        Assert.Contains(events, e => e.EventType == WorkflowLiveEventType.ExecutorCompleted);
        Assert.Equal([3], (await store.GetLiveEventsAsync(runId, 2)).Select(e => e.Sequence));
    }

    private sealed class InMemoryLiveEventStore : IWorkflowRunStore
    {
        private readonly List<WorkflowLiveEventDto> _events = [];

        public Task RecordLiveEventAsync(WorkflowLiveEventDto liveEvent, string? safeDataJson,
                                         CancellationToken ct = default)
        {
            _events.Add(liveEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WorkflowLiveEventDto>> GetLiveEventsAsync(Guid workflowRunId, long afterSequence = 0,
                                                                            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkflowLiveEventDto>>(
                _events.Where(e => e.WorkflowRunId == workflowRunId && e.Sequence > afterSequence)
                    .OrderBy(e => e.Sequence)
                    .ToArray());

        public Task StartAsync(Guid runId, TravelPlanRequest request, string originalRequest, DateTimeOffset startedAt,
                               CancellationToken ct = default) => Task.CompletedTask;

        public Task RecordExecutorAsync(SmartTravelPlanner.Api.Models.Execution.ExecutorExecutionTrace trace,
                                        CancellationToken ct = default) => Task.CompletedTask;

        public Task SaveDiagnosticsAsync(Guid runId, SmartTravelPlanner.Api.Models.Execution.ExecutionTrace trace,
                                         object? tripPlan, CancellationToken ct = default) => Task.CompletedTask;

        public Task CompleteAsync(Guid runId, string status, DateTimeOffset completedAt, string? failureStage = null,
                                  string? error = null, CancellationToken ct = default) => Task.CompletedTask;

        public Task<PagedWorkflowRunsDto>
        ListAsync(WorkflowRunQuery query, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<WorkflowRunDetailsDto?>
        GetAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<WorkflowRunComparisonDto?>
        CompareAsync(Guid a, Guid b, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();

        public Task RecordAgentExecutionAsync(Guid runId, string executorName, AgentFailure? failure,
                                              AgentExecutionMetadata metadata, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class NoopHubContext : IHubContext<WorkflowEventHub>
    {
        public IHubClients Clients { get; } = new NoopClients();
        public IGroupManager Groups { get; } = new NoopGroups();
    }

    private sealed class NoopClients : IHubClients
    {
        public IClientProxy All => new NoopProxy();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new NoopProxy();
        public IClientProxy Client(string connectionId) => new NoopProxy();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new NoopProxy();
        public IClientProxy Group(string groupName) => new NoopProxy();
        public IClientProxy GroupExcept(string groupName,
                                        IReadOnlyList<string> excludedConnectionIds) => new NoopProxy();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new NoopProxy();
        public IClientProxy User(string userId) => new NoopProxy();
        public IClientProxy Users(IReadOnlyList<string> userIds) => new NoopProxy();
    }

    private sealed class NoopGroups : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName,
                                    CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName,
                                         CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoopProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args,
                                  CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
