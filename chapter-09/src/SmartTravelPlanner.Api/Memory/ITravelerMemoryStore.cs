using SmartTravelPlanner.Api.Models.Memory;
namespace SmartTravelPlanner.Api.Memory;
public interface ITravelerMemoryStore
{
    TravelerMemory? Get(Guid travelerId);
    TravelerMemory Upsert(TravelerMemory memory);
    bool Delete(Guid travelerId);
}
