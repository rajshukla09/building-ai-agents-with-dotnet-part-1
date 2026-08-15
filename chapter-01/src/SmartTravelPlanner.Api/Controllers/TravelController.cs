using Microsoft.AspNetCore.Mvc;
using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Contracts;
using Swashbuckle.AspNetCore.Annotations;

namespace SmartTravelPlanner.Api.Controllers;

[ApiController]
[Route("api/travel")]
[Produces("application/json")]
public sealed class TravelController : ControllerBase
{
    private readonly ITravelAgent _travelAgent;
    private readonly ILogger<TravelController> _logger;

    public TravelController(ITravelAgent travelAgent, ILogger<TravelController> logger)
    {
        _travelAgent = travelAgent;
        _logger = logger;
    }

    [HttpPost("plan", Name = "CreateTravelPlan")]
    [Consumes("application/json")]
    [SwaggerOperation(
        Summary = "Creates a concise travel itinerary from a prompt",
        Description = "Runs one standalone TravelAgent interaction using the supplied travel request.")]
    [ProducesResponseType(typeof(TravelPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TravelPlanResponse>> CreatePlanAsync(
        [FromBody] TravelPlanRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received a travel-plan request");

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            ModelState.AddModelError(
                nameof(request.Prompt),
                "Prompt is required and cannot contain only whitespace.");
            return ValidationProblem(ModelState);
        }

        string response = await _travelAgent.CreateItineraryAsync(request.Prompt, cancellationToken);
        return Ok(new TravelPlanResponse(response));
    }
}
