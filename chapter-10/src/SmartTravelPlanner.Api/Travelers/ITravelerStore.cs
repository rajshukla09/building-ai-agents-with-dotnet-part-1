namespace SmartTravelPlanner.Api.Travelers;

public interface ITravelerStore
{
    TravelerProfile Add();

    TravelerProfile? Get(Guid travelerId);

    bool Exists(Guid travelerId);

    bool Delete(Guid travelerId);
}
