using Microsoft.AspNetCore.Mvc;
using SmartTravelPlanner.Api.Contracts;
using SmartTravelPlanner.Api.Memory;
using SmartTravelPlanner.Api.Models.Memory;
using SmartTravelPlanner.Api.Travelers;

namespace SmartTravelPlanner.Api.Controllers;

[ApiController]
[Route("api/travelers/{travelerId:guid}/memory")]
public sealed class TravelerMemoryController(TravelerMemoryService memoryService, ITravelerStore travelerStore) : ControllerBase
{
    [HttpGet]
    public ActionResult<TravelerMemory> Get(Guid travelerId) =>
        travelerStore.Exists(travelerId) && memoryService.Get(travelerId) is { } memory ? Ok(memory) : NotFound();

    [HttpPut]
    public ActionResult<TravelerMemory> Put(Guid travelerId, TravelerMemoryRequest request) =>
        travelerStore.Exists(travelerId) ? Ok(memoryService.Save(travelerId, request)) : NotFound();

    [HttpDelete]
    public IActionResult Delete(Guid travelerId)
    {
        if (!travelerStore.Exists(travelerId))
            return NotFound();
        return memoryService.Delete(travelerId) ? NoContent() : NotFound();
    }
}
