using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Agents.Results;
using SmartTravelPlanner.Api.Configuration;
using SmartTravelPlanner.Api.Conversations;
using SmartTravelPlanner.Api.Memory;
using SmartTravelPlanner.Api.Models.Execution;
using SmartTravelPlanner.Api.Models.Memory;
using SmartTravelPlanner.Api.Models.TravelPlanning;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class TravelerMemoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"traveler-memory-{Guid.NewGuid():N}");
    private string FilePath => Path.Combine(_directory, "memories.json");

    [Fact]
    public void SavesAndRetrievesExplicitPreferences()
    {
        Guid travelerId = Guid.NewGuid();
        TravelerMemoryService service = CreateService();
        TravelerMemory saved =
            service.Save(travelerId, new(["vegetarian"], ["museums"], "relaxed", "mid-range", ["step-free access"]));

        Assert.Equal(travelerId, saved.TravelerId);
        Assert.Equal("vegetarian", Assert.Single(service.Get(travelerId)!.FoodPreferences));
        Assert.Contains("relaxed", service.BuildContext(travelerId));
    }

    [Fact]
    public void RestoresMemoryAfterStoreRestart()
    {
        Guid travelerId = Guid.NewGuid();
        CreateService().Save(travelerId, new(FoodPreferences: ["vegetarian"]));

        TravelerMemoryService restarted = CreateService();
        Assert.Equal("vegetarian", Assert.Single(restarted.Get(travelerId)!.FoodPreferences));
    }

    [Fact]
    public void UpdatingReplacesExistingPreferences()
    {
        Guid travelerId = Guid.NewGuid();
        TravelerMemoryService service = CreateService();
        service.Save(travelerId, new(FoodPreferences: ["vegetarian"], TravelPace: "relaxed"));
        service.Save(travelerId, new(FoodPreferences: ["vegan"], TravelPace: "active"));

        TravelerMemory memory = service.Get(travelerId)!;
        Assert.Equal("vegan", Assert.Single(memory.FoodPreferences));
        Assert.Equal("active", memory.TravelPace);
    }

    [Fact]
    public void DeletesMemoryWithoutAffectingOtherTravelers()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        TravelerMemoryService service = CreateService();
        service.Save(first, new(FoodPreferences: ["vegetarian"]));
        service.Save(second, new(FoodPreferences: ["seafood"]));

        Assert.True(service.Delete(first));
        Assert.Null(service.Get(first));
        Assert.Equal("seafood", Assert.Single(service.Get(second)!.FoodPreferences));
    }

    [Fact]
    public void ContextContainsOnlyRequestedTravelersMemory()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        TravelerMemoryService service = CreateService();
        service.Save(first, new(FoodPreferences: ["vegetarian"]));
        service.Save(second, new(FoodPreferences: ["seafood"]));

        Assert.Contains("vegetarian", service.BuildContext(first));
        Assert.DoesNotContain("seafood", service.BuildContext(first));
    }

    [Fact]
    public async Task NewConversationUsesMemoryWhileConversationStateRemainsSeparate()
    {
        Guid travelerId = Guid.NewGuid();
        TravelerMemoryService memory = CreateService();
        memory.Save(travelerId, new(FoodPreferences: ["vegetarian"], TravelPace: "relaxed"));
        FakeTravelAgent agent = new();
        var lifecycle =
            Options.Create(new SessionLifecycleOptions { IdleTimeoutMinutes = 15, ExpirationTimeoutMinutes = 30 });
        var conversations = new InMemoryConversationStore(TimeProvider.System, lifecycle);
        var service = new ConversationService(agent, conversations, TimeProvider.System, lifecycle, memory);

        var first = await service.CreateAsync(travelerId);
        var second = await service.CreateAsync(travelerId);
        await service.SendMessageAsync(second.ConversationId, travelerId, "Plan a trip to Tokyo.");

        Assert.Equal("Plan a trip to Tokyo.", agent.LastMessage);
        Assert.Equal(0, service.Get(first.ConversationId)!.MessageCount);
        Assert.Equal(1, service.Get(second.ConversationId)!.MessageCount);
        Assert.Equal("vegetarian", Assert.Single(memory.Get(travelerId)!.FoodPreferences));
    }

    private sealed class FakeTravelAgent : ITravelAgent
    {
        public string LastMessage { get; private set; } = string.Empty;

        public Task<AgentSession>
        CreateSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult<AgentSession>(null!);

        public Task<TripPlanResponse>
        SendMessageAsync(string message, AgentSession session, CancellationToken cancellationToken = default,
                         SmartTravelPlanner.Api.Context.TravelInvocationContext? invocationContext = null)
        {
            LastMessage = message;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return Task.FromResult(new TripPlanResponse(
                new TripPlan { Destination = "Tokyo", DurationDays = 1, Summary = message,
                               Days = [new TripDay { DayNumber = 1, Title = "Tokyo",
                                                     Activities = [new TripActivity { Time = "09:00", Name = "Garden",
                                                                                      Description = "Visit",
                                                                                      Category = "Sightseeing",
                                                                                      Notes = "" }] }] },
                new ExecutionTrace { StartedAt = now, CompletedAt = now, TotalDurationMs = 0 }));
        }

        public Task<AgentResult<TripPlan>>
        ExecuteAsync(TravelAgentRequest request,
                     CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private TravelerMemoryService CreateService() =>
        new(new TravelerMemoryStore(FilePath, NullLogger<TravelerMemoryStore>.Instance), TimeProvider.System);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }
}
