using Microsoft.AspNetCore.Mvc;
using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Contracts;
using SmartTravelPlanner.Api.Models.TravelPlanning;
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
        Summary = "Creates a structured travel itinerary",
        Description = "Runs one standalone TravelAgent interaction using the supplied travel request.")]
    [ProducesResponseType(typeof(TripPlan), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TripPlan>> CreatePlanAsync(
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

        TripPlan response = await _travelAgent.CreateItineraryAsync(request, cancellationToken);
        return Ok(response);
    }
}
