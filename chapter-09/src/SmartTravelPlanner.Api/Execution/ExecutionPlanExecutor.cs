using System.Text.Json;
using SmartTravelPlanner.Api.Classification;
using SmartTravelPlanner.Api.Routing;

namespace SmartTravelPlanner.Api.Execution;

public sealed class ExecutionPlanExecutor(
    IToolRouter router,
    IToolExecutionPipeline pipeline,
    IExecutionTraceRecorder traceRecorder,
    TimeProvider timeProvider,
    ILogger<ExecutionPlanExecutor> logger) : IExecutionPlanExecutor
{
    public async Task<string> EnrichRequestAsync(
        string request, ExecutionPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Steps.Count == 0)
        {
            traceRecorder.RecordPlanExecutionDuration(0);
            return request;
        }

        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        List<StepResult> results = [];
        try
        {
            foreach (ExecutionStep step in plan.Steps.OrderBy(item => item.Order))
            {
                ToolRouteDecision route = router.Route(step);
                try
                {
                    object? output = await pipeline.ExecuteAsync(route, cancellationToken);
                    results.Add(new StepResult(step.Order, step.Tool, "Success", output, null));
                }
                catch (ToolExecutionFailedException exception)
                {
                    logger.LogWarning(exception, "Execution plan step {StepOrder} ({Tool}) failed; continuing plan", step.Order, step.Tool);
                    results.Add(new StepResult(step.Order, step.Tool, "Failure", null, exception.Message));
                }
            }
        }
        finally
        {
            traceRecorder.RecordPlanExecutionDuration(
                Math.Max(0, (long)(timeProvider.GetUtcNow() - startedAt).TotalMilliseconds));
        }
        return BuildEnrichedRequest(request, results);
    }

    internal static string BuildEnrichedRequest(string request, IReadOnlyList<StepResult> results) => $"""
        {request}

        The application executed the following deterministic plan in order.
        Execution results: {JsonSerializer.Serialize(results)}
        Use every successful result. Explicitly acknowledge failed steps without inventing data.
        Do not invoke these tools again.
        """;

    internal sealed record StepResult(int Order, ToolType Tool, string Status, object? Output, string? FailureReason);
}
