using System.Collections.Concurrent;
using System.Threading.Channels;
using SmartTravelPlanner.Api.Workflows;

namespace SmartTravelPlanner.Api.Live;

public sealed record QueuedWorkflowRun(Guid WorkflowRunId, TravelPlanRequest Request, string OriginalRequest);

public interface IWorkflowExecutionQueue
{
    ValueTask QueueAsync(QueuedWorkflowRun run, CancellationToken cancellationToken = default);

    ValueTask<QueuedWorkflowRun> DequeueAsync(CancellationToken cancellationToken);
}

public sealed class WorkflowExecutionQueue : IWorkflowExecutionQueue
{
    private readonly Channel<QueuedWorkflowRun> _queue = Channel.CreateUnbounded<QueuedWorkflowRun>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly ConcurrentDictionary<Guid, byte> _queued = new();

    public async ValueTask QueueAsync(QueuedWorkflowRun run, CancellationToken cancellationToken = default)
    {
        if (!_queued.TryAdd(run.WorkflowRunId, 0))
        {
            throw new InvalidOperationException($"Workflow run {run.WorkflowRunId} is already queued.");
        }

        await _queue.Writer.WriteAsync(run, cancellationToken);
    }

    public async ValueTask<QueuedWorkflowRun> DequeueAsync(CancellationToken cancellationToken)
    {
        QueuedWorkflowRun run = await _queue.Reader.ReadAsync(cancellationToken);
        _queued.TryRemove(run.WorkflowRunId, out _);
        return run;
    }
}

public sealed class WorkflowExecutionBackgroundService(
    IWorkflowExecutionQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<WorkflowExecutionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            QueuedWorkflowRun run;
            try
            {
                run = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ITravelWorkflowService>();
                await service.ExecuteExistingRunAsync(run.WorkflowRunId, run.Request, run.OriginalRequest, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Workflow background execution cancelled during shutdown for {WorkflowRunId}", run.WorkflowRunId);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Workflow background execution failed for {WorkflowRunId}", run.WorkflowRunId);
            }
        }
    }
}
