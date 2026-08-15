using Microsoft.Extensions.Logging.Abstractions;
using SmartTravelPlanner.Api.Travelers;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class JsonTravelerStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"traveler-store-{Guid.NewGuid():N}");
    private string FilePath => Path.Combine(_directory, "travelers.json");

    [Fact]
    public void CreateGeneratesIdAndRestartRestoresProfile()
    {
        JsonTravelerStore store = CreateStore();
        TravelerProfile created = store.Add();

        Assert.NotEqual(Guid.Empty, created.TravelerId);
        Assert.Equal(created, CreateStore().Get(created.TravelerId));
    }

    [Fact]
    public void DeleteRemovesOnlyIdentity()
    {
        JsonTravelerStore store = CreateStore();
        TravelerProfile first = store.Add();
        TravelerProfile second = store.Add();

        Assert.True(store.Delete(first.TravelerId));
        Assert.Null(store.Get(first.TravelerId));
        Assert.NotNull(store.Get(second.TravelerId));
    }

    private JsonTravelerStore CreateStore() => new(FilePath, TimeProvider.System, NullLogger<JsonTravelerStore>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }
}
