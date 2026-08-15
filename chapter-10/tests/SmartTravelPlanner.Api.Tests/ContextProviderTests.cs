using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using SmartTravelPlanner.Api.Context;
using SmartTravelPlanner.Api.Memory;
using SmartTravelPlanner.Api.Models.Conversations;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class ContextProviderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"context-provider-{Guid.NewGuid():N}");

    [Fact]
    public void MemoryProviderInjectsOnlyCurrentTravelerPreferences()
    {
        TravelerMemoryService memory = CreateMemoryService();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        memory.Save(first, new(FoodPreferences: ["vegetarian"], TravelPace: "relaxed"));
        memory.Save(second, new(FoodPreferences: ["seafood"]));
        var accessor = new TravelInvocationContextAccessor();
        using (accessor.Push(new TravelInvocationContext(TravelerId: first)))
        {
            ChatMessage context = Assert.Single(
                TravelerMemoryContextProvider.AppendMemoryContext([], accessor, memory));
            Assert.Contains("vegetarian", context.Text);
            Assert.Contains("relaxed", context.Text);
            Assert.DoesNotContain("seafood", context.Text);
        }
    }

    [Fact]
    public void MissingMemoryAddsNoContextAndDoesNotFail()
    {
        var accessor = new TravelInvocationContextAccessor();
        using (accessor.Push(new TravelInvocationContext(TravelerId: Guid.NewGuid())))
        {
            Assert.Empty(TravelerMemoryContextProvider.AppendMemoryContext([], accessor, CreateMemoryService()));
        }
    }

    [Fact]
    public void RuntimeProviderSuppliesInvocationMetadata()
    {
        Guid traveler = Guid.NewGuid();
        Guid conversation = Guid.NewGuid();
        var accessor = new TravelInvocationContextAccessor();
        using (accessor.Push(new TravelInvocationContext(traveler, conversation, SessionStatus.Active, "Tokyo", 3)))
        {
            string instructions = Assert.Single(
                RuntimeTravelContextProvider.AppendRuntimeContext([], accessor, TimeProvider.System)).Text!;
            Assert.Contains(traveler.ToString(), instructions);
            Assert.Contains(conversation.ToString(), instructions);
            Assert.Contains("Tokyo", instructions);
            Assert.Contains("3 days", instructions);
        }
    }

    private TravelerMemoryService CreateMemoryService() => new(
        new TravelerMemoryStore(Path.Combine(_directory, "memory.json"), NullLogger<TravelerMemoryStore>.Instance),
        TimeProvider.System);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
