using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Agents.Results;
using SmartTravelPlanner.Api.Configuration;
using SmartTravelPlanner.Api.Conversations;
using SmartTravelPlanner.Api.Models.Conversations;
using SmartTravelPlanner.Api.Models.Execution;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class JsonConversationStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"travel-planner-{Guid.NewGuid():N}");
    private readonly DateTimeOffset _now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Add_PersistsJson_AndRestartRestoresAllMetadata()
    {
        TestClock clock = new(_now);
        JsonConversationStore first = CreateStore(clock);
        ConversationState one = first.Add(null!);
        one.RecordMessage(_now.AddMinutes(1), Options.Value);
        first.Update(one);
        ConversationState two = first.Add(null!);
        two.Expire(_now.AddMinutes(2));
        first.Update(two);

        Assert.True(File.Exists(FilePath));
        using (JsonDocument json = JsonDocument.Parse(File.ReadAllText(FilePath)))
        {
            Assert.Equal(2, json.RootElement.GetProperty("conversations").GetArrayLength());
        }

        JsonConversationStore restarted = CreateStore(clock);
        Assert.Equal(2, restarted.GetAll().Count);
        ConversationMetadata restoredOne = AssertRestored(restarted, one.Id);
        Assert.Equal(1, restoredOne.MessageCount);
        Assert.Equal(SessionStatus.Active, restoredOne.Status);
        Assert.Equal(_now.AddMinutes(1).Add(Options.Value.ExpirationTimeout), restoredOne.ExpirationTime);
        Assert.Equal(SessionStatus.Expired, AssertRestored(restarted, two.Id).Status);
    }

    [Fact]
    public void Delete_RemovesConversationFromPersistence()
    {
        JsonConversationStore store = CreateStore(new TestClock(_now));
        ConversationState state = store.Add(null!);

        Assert.True(store.Delete(state.Id));

        Assert.Empty(CreateStore(new TestClock(_now)).GetAll());
    }

    [Fact]
    public void CleanupExpired_RemovesConversationFromPersistence()
    {
        TestClock clock = new(_now);
        JsonConversationStore store = CreateStore(clock);
        ConversationState state = store.Add(null!);
        state.Expire(_now);
        store.Update(state);
        ConversationService service = new(new UnusedTravelAgent(), store, clock, Options);

        Assert.Equal(1, service.CleanupExpired());

        Assert.Empty(CreateStore(clock).GetAll());
    }

    [Fact]
    public void MissingAndEmptyFiles_StartWithEmptyStore()
    {
        Assert.Empty(CreateStore(new TestClock(_now)).GetAll());
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, string.Empty);
        Assert.Empty(CreateStore(new TestClock(_now)).GetAll());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"version\":1,\"conversations\":[{\"id\":17}]}")]
    public void CorruptPersistence_IsIgnored(string contents)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, contents);

        Assert.Empty(CreateStore(new TestClock(_now)).GetAll());
    }

    private string FilePath => Path.Combine(_directory, "conversations.json");

    private static IOptions<SessionLifecycleOptions> Options => Microsoft.Extensions.Options.Options.Create(
        new SessionLifecycleOptions { IdleTimeoutMinutes = 5, ExpirationTimeoutMinutes = 30 });

    private JsonConversationStore CreateStore(TimeProvider clock) => new(
        clock, Options, new TestSessionSerializer(), NullLogger<JsonConversationStore>.Instance, FilePath);

    private ConversationMetadata AssertRestored(JsonConversationStore store, Guid id)
    {
        Assert.True(store.TryGet(id, out ConversationState? state));
        return state.ToMetadata(_now, Options.Value);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private sealed class TestSessionSerializer : IAgentSessionSerializer
    {
        public JsonElement SerializeSession(AgentSession session) => JsonSerializer.SerializeToElement(new
        {
            kind = "test"
        });

        public AgentSession DeserializeSession(JsonElement session) => null!;
    }

    private sealed class UnusedTravelAgent : ITravelAgent
    {
        public Task<AgentResult<SmartTravelPlanner.Api.Models.TravelPlanning.TripPlan>> ExecuteAsync(
            TravelAgentRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AgentSession> CreateSessionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TripPlanResponse> SendMessageAsync(
            string message,
            AgentSession session,
            CancellationToken cancellationToken = default,
            SmartTravelPlanner.Api.Context.TravelInvocationContext? invocationContext = null) =>
            throw new NotSupportedException();
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
