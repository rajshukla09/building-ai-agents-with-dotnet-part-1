using Microsoft.AspNetCore.Mvc;
using SmartTravelPlanner.Api.Workflows;
using Swashbuckle.AspNetCore.Annotations;
using TripPlanResponse = SmartTravelPlanner.Api.Models.Execution.TripPlanResponse;

namespace SmartTravelPlanner.Api.Controllers;

[ApiController]
[Route("api/travel")]
[Produces("application/json")]
public sealed class TravelController : ControllerBase
{
    private readonly ITravelWorkflowService _workflowService;
    private readonly ILogger<TravelController> _logger;

    public TravelController(ITravelWorkflowService workflowService, ILogger<TravelController> logger)
    {
        _workflowService = workflowService;
        _logger = logger;
    }

    [HttpPost("plan", Name = "CreateTravelPlan")]
    [Consumes("application/json")]
    [SwaggerOperation(
        Summary = "Creates a structured travel itinerary",
        Description = "Runs the sequential travel-planning workflow using the supplied request.")]
    [ProducesResponseType(typeof(TripPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TripPlanResponse>> CreatePlanAsync(
        [FromBody] TravelPlanRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received a travel-plan request");

        if (string.IsNullOrWhiteSpace(request.Destination))
        {
            ModelState.AddModelError(
                nameof(request.Destination),
                "Destination is required and cannot contain only whitespace.");
            return ValidationProblem(ModelState);
        }

        TripPlanResponse response = await _workflowService.ExecuteAsync(request, cancellationToken);
        return Ok(response);
    }
}
