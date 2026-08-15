using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using SmartTravelPlanner.Contracts;

namespace SmartTravelPlanner.Client.Services;

public interface IWorkflowLiveClient : IAsyncDisposable
{
    string ConnectionStatus
    {
        get;
    }

    event Func<WorkflowLiveEventDto, Task>? EventReceived;

    Task ConnectAsync(Guid workflowRunId, CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}

public sealed class SignalRWorkflowLiveClient(HttpClient httpClient) : IWorkflowLiveClient
{
    private HubConnection? _connection;
    private Guid? _subscribedRunId;
    public string ConnectionStatus { get; private set; } = "Disconnected";

    public event Func<WorkflowLiveEventDto, Task>? EventReceived;

    public async Task ConnectAsync(Guid workflowRunId, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();
        _subscribedRunId = workflowRunId;
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(httpClient.BaseAddress!, "hubs/workflow-events"))
            .WithAutomaticReconnect()
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
        _connection.Reconnecting += _ =>
        {
            ConnectionStatus = "Reconnecting";
            return Task.CompletedTask;
        };
        _connection.Reconnected += async _ =>
        {
            ConnectionStatus = "Connected";
            if (_subscribedRunId is Guid id)
                await _connection.InvokeAsync("SubscribeToRun", id, cancellationToken);
        };
        _connection.Closed += _ => {
            ConnectionStatus = "Disconnected";
            return Task.CompletedTask; };
        _connection.On<WorkflowLiveEventDto>("WorkflowEventReceived", async liveEvent =>
        {
            if (EventReceived is not null)
                await EventReceived.Invoke(liveEvent);
        });
        ConnectionStatus = "Connecting";
        await _connection.StartAsync(cancellationToken);
        await _connection.InvokeAsync("SubscribeToRun", workflowRunId, cancellationToken);
        ConnectionStatus = "Connected";
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null)
            return;
        if (_subscribedRunId is Guid runId)
            await _connection.InvokeAsync("UnsubscribeFromRun", runId);
        await _connection.DisposeAsync();
        _connection = null;
        ConnectionStatus = "Disconnected";
    }

    public ValueTask DisposeAsync() => _connection?.DisposeAsync() ?? ValueTask.CompletedTask;
}
