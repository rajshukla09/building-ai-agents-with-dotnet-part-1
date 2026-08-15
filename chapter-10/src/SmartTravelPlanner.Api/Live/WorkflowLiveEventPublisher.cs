using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using SmartTravelPlanner.Api.Hubs;
using SmartTravelPlanner.Api.Persistence;
using SmartTravelPlanner.Contracts;

namespace SmartTravelPlanner.Api.Live;

public interface IWorkflowLiveEventPublisher
{
    Task<WorkflowLiveEventDto> PublishAsync(Guid runId, WorkflowLiveEventType eventType, WorkflowStageType stageType,
        string stageName, WorkflowStageStatus status, string? inputMessageType = null, string? outputMessageType = null,
        long? durationMs = null, string? summary = null, object? data = null, CancellationToken cancellationToken = default);
}

public sealed class SignalRWorkflowLiveEventPublisher(
    IHubContext<WorkflowEventHub> hubContext,
    IWorkflowRunStore store,
    TimeProvider timeProvider) : IWorkflowLiveEventPublisher
{
    private readonly ConcurrentDictionary<Guid, long> _sequences = new();

    public async Task<WorkflowLiveEventDto> PublishAsync(Guid runId, WorkflowLiveEventType eventType, WorkflowStageType stageType,
        string stageName, WorkflowStageStatus status, string? inputMessageType = null, string? outputMessageType = null,
        long? durationMs = null, string? summary = null, object? data = null, CancellationToken cancellationToken = default)
    {
        long sequence = _sequences.AddOrUpdate(runId, 1, (_, current) => current + 1);
        var liveEvent = new WorkflowLiveEventDto(runId, sequence, timeProvider.GetUtcNow(), eventType, stageType,
            stageName, status, inputMessageType, outputMessageType, durationMs, summary, data);
        await store.RecordLiveEventAsync(liveEvent, JsonSerializer.Serialize(data), cancellationToken);
        await hubContext.Clients.Group(runId.ToString()).SendAsync("WorkflowEventReceived", liveEvent, cancellationToken);
        return liveEvent;
    }
}
