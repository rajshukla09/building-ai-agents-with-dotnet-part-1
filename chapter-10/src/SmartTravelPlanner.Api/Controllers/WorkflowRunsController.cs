using Microsoft.AspNetCore.Mvc;
using SmartTravelPlanner.Api.Live;
using SmartTravelPlanner.Api.Persistence;
using SmartTravelPlanner.Api.Workflows;
using SmartTravelPlanner.Contracts;

namespace SmartTravelPlanner.Api.Controllers;

[ApiController]
[Route("api/workflow-runs")]
public sealed class WorkflowRunsController(IWorkflowRunStore store, IWorkflowExecutionQueue queue,
                                           TimeProvider timeProvider)
    : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<StartWorkflowRunResponse>> StartAsync([FromBody] TravelPlanRequest request,
                                                                         CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Destination))
        {
            ModelState.AddModelError(nameof(request.Destination),
                                     "Destination is required and cannot contain only whitespace.");
            return ValidationProblem(ModelState);
        }

        Guid runId = Guid.NewGuid();
        string originalRequest = TravelWorkflowService.OriginalRequest(request);
        await store.StartAsync(runId, request, originalRequest, timeProvider.GetUtcNow(), cancellationToken);
        await queue.QueueAsync(new QueuedWorkflowRun(runId, request, originalRequest), cancellationToken);
        return Accepted(new StartWorkflowRunResponse(runId, "Queued"));
    }

    [HttpGet]
    public Task<PagedWorkflowRunsDto>
    ListAsync([FromQuery] string? status, [FromQuery] string? destination, [FromQuery] bool? repairAttempted,
              [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] int page = 1,
              [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        store.ListAsync(new WorkflowRunQuery(status, destination, repairAttempted, from, to, page, pageSize),
                        cancellationToken);

    [HttpGet("{workflowRunId:guid}")]
    public async Task<ActionResult<WorkflowRunDetailsDto>> GetAsync(Guid workflowRunId,
                                                                    CancellationToken cancellationToken)
    {
        WorkflowRunDetailsDto? run = await store.GetAsync(workflowRunId, cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpGet("{workflowRunId:guid}/events")]
    public async Task<IReadOnlyList<WorkflowLiveEventDto>> EventsAsync(Guid workflowRunId,
                                                                       [FromQuery] long afterSequence = 0,
                                                                       CancellationToken cancellationToken = default) =>
        await store.GetLiveEventsAsync(workflowRunId, afterSequence, cancellationToken);

    [HttpGet("{workflowRunId:guid}/compare/{otherWorkflowRunId:guid}")]
    public async Task<ActionResult<WorkflowRunComparisonDto>> CompareAsync(Guid workflowRunId, Guid otherWorkflowRunId,
                                                                           CancellationToken cancellationToken)
    {
        WorkflowRunComparisonDto? comparison =
            await store.CompareAsync(workflowRunId, otherWorkflowRunId, cancellationToken);
        return comparison is null ? NotFound() : Ok(comparison);
    }

    [HttpDelete("{workflowRunId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid workflowRunId, CancellationToken cancellationToken)
    {
        bool deleted = await store.DeleteAsync(workflowRunId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
