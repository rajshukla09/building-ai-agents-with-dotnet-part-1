using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Configuration;
using SmartTravelPlanner.Api.Contracts;
using SmartTravelPlanner.Api.Conversations;
using SmartTravelPlanner.Api.Models.Conversations;
using SmartTravelPlanner.Api.Models.TravelPlanning;
using SmartTravelPlanner.Api.Models.Execution;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class SessionLifecycleTests
{
    private static readonly SessionLifecycleOptions LifecycleOptions = new()
    {
        IdleTimeoutMinutes = 10,
        ExpirationTimeoutMinutes = 30
    };

    [Fact]
    public void ActiveSessionBecomesIdleThenExpiredUsingConfiguredTimeouts()
    {
        ManualTimeProvider clock = new();
        ConversationState session = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null!,
            clock.GetUtcNow(),
            LifecycleOptions.ExpirationTimeout);

        session.RecordMessage(clock.GetUtcNow(), LifecycleOptions);
        Assert.Equal(SessionStatus.Active, session.RefreshStatus(clock.GetUtcNow(), LifecycleOptions));

        clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(SessionStatus.Idle, session.RefreshStatus(clock.GetUtcNow(), LifecycleOptions));

        clock.Advance(TimeSpan.FromMinutes(20));
        Assert.Equal(SessionStatus.Expired, session.RefreshStatus(clock.GetUtcNow(), LifecycleOptions));
    }

    [Fact]
    public async Task ExpiredSessionRejectsMessagesWithoutInvokingAgent()
    {
        TestContext context = new();
        ConversationMetadata session = await context.Service.CreateAsync();
        context.Clock.Advance(LifecycleOptions.ExpirationTimeout);

        SendMessageResult result = await context.Service.SendMessageAsync(session.ConversationId, "Plan Jaipur.");

        Assert.Equal(SendMessageOutcome.Expired, result.Outcome);
        Assert.Equal(0, context.Agent.MessageCallCount);
    }

    [Fact]
    public async Task CleanupRemovesOnlyExpiredSessions()
    {
        TestContext context = new();
        ConversationMetadata expired = await context.Service.CreateAsync();
        ConversationMetadata active = await context.Service.CreateAsync();
        Assert.True(context.Service.Expire(expired.ConversationId));

        int removed = context.Service.CleanupExpired();

        Assert.Equal(1, removed);
        Assert.Null(context.Service.Get(expired.ConversationId));
        Assert.NotNull(context.Service.Get(active.ConversationId));
    }

    [Fact]
    public async Task MultipleConcurrentSessionsRemainIndependent()
    {
        TestContext context = new();
        ConversationMetadata[] sessions = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => context.Service.CreateAsync()));

        SendMessageResult[] results = await Task.WhenAll(sessions.Select(
            (session, index) => context.Service.SendMessageAsync(session.ConversationId, $"Trip {index}")));

        Assert.Equal(10, sessions.Select(session => session.ConversationId).Distinct().Count());
        Assert.All(results, result => Assert.Equal(SendMessageOutcome.Success, result.Outcome));
        Assert.All(sessions, session =>
        {
            ConversationMetadata metadata = context.Service.Get(session.ConversationId)!;
            Assert.Equal(1, metadata.MessageCount);
            Assert.Equal(SessionStatus.Active, metadata.Status);
        });
    }

    [Fact]
    public async Task DeletedSessionCannotBeRetrievedOrMessaged()
    {
        TestContext context = new();
        ConversationMetadata session = await context.Service.CreateAsync();

        Assert.True(context.Service.Delete(session.ConversationId));
        Assert.Null(context.Service.Get(session.ConversationId));
        SendMessageResult result = await context.Service.SendMessageAsync(session.ConversationId, "Follow up");
        Assert.Equal(SendMessageOutcome.NotFound, result.Outcome);
    }

    private sealed class TestContext
    {
        public TestContext()
        {
            IOptions<SessionLifecycleOptions> options = Options.Create(LifecycleOptions);
            Store = new InMemoryConversationStore(Clock, options);
            Service = new ConversationService(Agent, Store, Clock, options);
        }

        public ManualTimeProvider Clock { get; } = new();
        public FakeTravelAgent Agent { get; } = new();
        public InMemoryConversationStore Store { get; }
        public ConversationService Service { get; }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 8, 2, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    private sealed class FakeTravelAgent : ITravelAgent
    {
        private int _messageCallCount;

        public int MessageCallCount => _messageCallCount;

        public Task<AgentSession> CreateSessionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<AgentSession>(null!);

        public Task<TripPlanResponse> SendMessageAsync(
            string message,
            AgentSession session,
            CancellationToken cancellationToken = default,
            SmartTravelPlanner.Api.Context.TravelInvocationContext? invocationContext = null)
        {
            Interlocked.Increment(ref _messageCallCount);
            return Task.FromResult(CreateResponse(message));
        }

        public Task<TripPlanResponse> CreateItineraryAsync(
            TravelPlanRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResponse(request.Destination));

        private static TripPlanResponse CreateResponse(string summary)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return new(CreatePlan(summary), new ExecutionTrace
            {
                StartedAt = now,
                CompletedAt = now,
                TotalDurationMs = 0
            });
        }

        private static TripPlan CreatePlan(string summary) => new()
        {
            Destination = "Test destination",
            DurationDays = 1,
            Summary = summary,
            Days =
            [
                new TripDay
                {
                    DayNumber = 1,
                    Title = "Test day",
                    Activities =
                    [
                        new TripActivity
                        {
                            Time = "09:00",
                            Name = "Test activity",
                            Description = "Test description",
                            Category = "Sightseeing",
                            Notes = ""
                        }
                    ]
                }
            ]
        };
    }
}
