using Microsoft.AspNetCore.Mvc;
using SmartTravelPlanner.Api.Live;
using SmartTravelPlanner.Contracts;

namespace SmartTravelPlanner.Api.Controllers;

[ApiController]
[Route("api/workflows")]
public sealed class WorkflowsController : ControllerBase
{
    [HttpGet("travel-planning/topology")]
    public ActionResult<WorkflowTopologyDto> GetTravelPlanningTopology() => Ok(WorkflowTopologyProvider.TravelPlanning());
}
