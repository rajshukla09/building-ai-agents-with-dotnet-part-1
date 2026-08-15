using Microsoft.AspNetCore.Mvc;
using SmartTravelPlanner.Api.Memory;
using SmartTravelPlanner.Api.Travelers;

namespace SmartTravelPlanner.Api.Controllers;

[ApiController]
[Route("api/travelers")]
public sealed class TravelersController(ITravelerStore travelerStore, TravelerMemoryService memoryService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(TravelerProfile), StatusCodes.Status201Created)]
    public ActionResult<TravelerProfile> Create()
    {
        TravelerProfile profile = travelerStore.Add();
        return CreatedAtAction(nameof(Get), new
        {
            travelerId = profile.TravelerId
        }, profile);
    }

    [HttpGet("{travelerId:guid}")]
    [ProducesResponseType(typeof(TravelerProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<TravelerProfile> Get(Guid travelerId) =>
        travelerStore.Get(travelerId) is { } profile ? Ok(profile) : NotFound();

    [HttpDelete("{travelerId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid travelerId)
    {
        if (!travelerStore.Delete(travelerId))
            return NotFound();
        memoryService.Delete(travelerId);
        return NoContent();
    }
}
