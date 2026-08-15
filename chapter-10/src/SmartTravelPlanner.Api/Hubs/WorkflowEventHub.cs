using Microsoft.AspNetCore.SignalR;

namespace SmartTravelPlanner.Api.Hubs;

public sealed class WorkflowEventHub : Hub
{
    public Task SubscribeToRun(Guid workflowRunId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, workflowRunId.ToString());

    public Task UnsubscribeFromRun(Guid workflowRunId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, workflowRunId.ToString());
}
