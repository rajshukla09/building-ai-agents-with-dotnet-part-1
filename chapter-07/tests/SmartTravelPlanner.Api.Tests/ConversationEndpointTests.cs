using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartTravelPlanner.Api.Contracts;
using SmartTravelPlanner.Api.Conversations;
using SmartTravelPlanner.Api.Models.Conversations;
using SmartTravelPlanner.Api.Models.TravelPlanning;
using SmartTravelPlanner.Api.Models.Execution;
using SmartTravelPlanner.Api.Travelers;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class ConversationEndpointTests
{
    [Fact]
    public async Task CreateConversationReturnsCreatedMetadata()
    {
        using TestApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync($"/api/conversations?travelerId={TestTravelerStore.Id}", null);
        ConversationMetadata? conversation = await response.Content.ReadFromJsonAsync<ConversationMetadata>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(conversation);
        Assert.NotEqual(Guid.Empty, conversation.ConversationId);
        Assert.Equal(0, conversation.MessageCount);
        Assert.Equal(SessionStatus.Created, conversation.Status);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task TravelerEndpointsCreateGetAndDeleteProfile()
    {
        using TestApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage createResponse = await client.PostAsync("/api/travelers", null);
        TravelerProfile? profile = await createResponse.Content.ReadFromJsonAsync<TravelerProfile>();
        HttpResponseMessage getResponse = await client.GetAsync($"/api/travelers/{profile!.TravelerId}");
        HttpResponseMessage deleteResponse = await client.DeleteAsync($"/api/travelers/{profile.TravelerId}");
        HttpResponseMessage deletedGetResponse = await client.GetAsync($"/api/travelers/{profile.TravelerId}");

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotEqual(Guid.Empty, profile.TravelerId);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deletedGetResponse.StatusCode);
    }

    [Fact]
    public async Task UnknownTravelerCannotCreateConversation()
    {
        using TestApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        Guid unknownId = Guid.NewGuid();
        HttpResponseMessage response = await client.PostAsync($"/api/conversations?travelerId={unknownId}", null);
        HttpResponseMessage memoryResponse = await client.PutAsJsonAsync(
            $"/api/travelers/{unknownId}/memory",
            new TravelerMemoryRequest(FoodPreferences: ["vegetarian"]));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, memoryResponse.StatusCode);
    }

    [Fact]
    public async Task MultipleMessagesReuseConversationContext()
    {
        using TestApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        ConversationMetadata conversation = await CreateConversationAsync(client);

        await SendMessageAsync(client, conversation.ConversationId, "Plan a three-day trip to Jaipur.");
        TripPlanResponse revised = await SendMessageAsync(client, conversation.ConversationId, "Make Day 2 less busy.");
        ConversationMetadata? metadata = await client.GetFromJsonAsync<ConversationMetadata>(
            $"/api/conversations/{conversation.ConversationId}");

        Assert.Contains("Plan a three-day trip to Jaipur.", revised.TripPlan.Summary);
        Assert.Contains("Make Day 2 less busy.", revised.TripPlan.Summary);
        Assert.Empty(revised.Execution.ToolCalls);
        Assert.Equal(2, metadata?.MessageCount);
    }

    [Fact]
    public async Task MessageResolvesTravelerFromConversationAndIgnoresLegacyTravelerField()
    {
        using TestApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        ConversationMetadata conversation = await CreateConversationAsync(client);

        using StringContent body = new(
            """{"message":"Plan Tokyo.","travelerId":{"legacy":"invalid-guid"}}""",
            System.Text.Encoding.UTF8,
            "application/json");
        HttpResponseMessage response = await client.PostAsync(
            $"/api/conversations/{conversation.ConversationId}/messages",
            body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ConversationsRemainIsolated()
    {
        using TestApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        ConversationMetadata first = await CreateConversationAsync(client);
        ConversationMetadata second = await CreateConversationAsync(client);

        await SendMessageAsync(client, first.ConversationId, "Plan Jaipur.");
        TripPlanResponse secondResponse = await SendMessageAsync(client, second.ConversationId, "Plan Kyoto.");

        Assert.Contains("Plan Kyoto.", secondResponse.TripPlan.Summary);
        Assert.DoesNotContain("Jaipur", secondResponse.TripPlan.Summary);
    }

    [Fact]
    public async Task UnknownConversationReturnsNotFound()
    {
        using TestApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        Guid unknownId = Guid.NewGuid();

        HttpResponseMessage getResponse = await client.GetAsync($"/api/conversations/{unknownId}");
        HttpResponseMessage messageResponse = await client.PostAsJsonAsync(
            $"/api/conversations/{unknownId}/messages",
            new ConversationMessageRequest("Plan Jaipur."));

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, messageResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteConversationRemovesIt()
    {
        using TestApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        ConversationMetadata conversation = await CreateConversationAsync(client);

        HttpResponseMessage deleteResponse = await client.DeleteAsync(
            $"/api/conversations/{conversation.ConversationId}");
        HttpResponseMessage getResponse = await client.GetAsync(
            $"/api/conversations/{conversation.ConversationId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task SessionEndpointsListExpireAndCleanUpSessions()
    {
        using TestApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        ConversationMetadata session = await CreateConversationAsync(client);

        ConversationMetadata[]? sessions = await client.GetFromJsonAsync<ConversationMetadata[]>("/api/sessions");
        HttpResponseMessage expireResponse = await client.PostAsync(
            $"/api/sessions/{session.ConversationId}/expire",
            null);
        HttpResponseMessage messageResponse = await client.PostAsJsonAsync(
            $"/api/conversations/{session.ConversationId}/messages",
            new ConversationMessageRequest("Follow up"));
        SessionCleanupResult? cleanup = await (await client.PostAsync("/api/sessions/cleanup", null))
            .Content.ReadFromJsonAsync<SessionCleanupResult>();

        Assert.Contains(sessions!, item => item.ConversationId == session.ConversationId);
        Assert.Equal(HttpStatusCode.NoContent, expireResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, messageResponse.StatusCode);
        Assert.Equal(1, cleanup?.RemovedCount);
    }

    private static async Task<ConversationMetadata> CreateConversationAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsync($"/api/conversations?travelerId={TestTravelerStore.Id}", null);
        return (await response.Content.ReadFromJsonAsync<ConversationMetadata>())!;
    }

    private static async Task<TripPlanResponse> SendMessageAsync(HttpClient client, Guid id, string message)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/conversations/{id}/messages",
            new ConversationMessageRequest(message));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TripPlanResponse>())!;
    }

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AzureOpenAI:Endpoint"] = "https://example.openai.azure.com/",
                    ["AzureOpenAI:ApiKey"] = "test-key",
                    ["AzureOpenAI:DeploymentName"] = "test-deployment"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IConversationService>();
                services.AddSingleton<IConversationService, FakeConversationService>();
                services.RemoveAll<ITravelerStore>();
                services.AddSingleton<ITravelerStore, TestTravelerStore>();
            });
        }
    }

    private sealed class TestTravelerStore : ITravelerStore
    {
        public static readonly Guid Id = Guid.Parse("8ebc9a71-3ad2-4f61-8cf7-e08e7ad89180");
        private bool _exists = true;
        public TravelerProfile Add() { _exists = true; return new() { TravelerId = Id, CreatedAt = DateTimeOffset.UtcNow }; }
        public TravelerProfile? Get(Guid travelerId) => Exists(travelerId) ? new TravelerProfile { TravelerId = Id, CreatedAt = DateTimeOffset.UtcNow } : null;
        public bool Exists(Guid travelerId) => _exists && travelerId == Id;
        public bool Delete(Guid travelerId) { if (!Exists(travelerId)) return false; _exists = false; return true; }
    }

    private sealed class FakeConversationService : IConversationService
    {
        private readonly ConcurrentDictionary<Guid, TestConversation> _conversations = new();

        public Task<ConversationMetadata> CreateAsync(CancellationToken cancellationToken = default)
            => CreateAsync(TestTravelerStore.Id, cancellationToken);

        public Task<ConversationMetadata> CreateAsync(
            Guid travelerId,
            CancellationToken cancellationToken = default)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TestConversation conversation = new(Guid.NewGuid(), travelerId, now);
            _conversations[conversation.Id] = conversation;
            return Task.FromResult(conversation.Metadata);
        }

        public ConversationMetadata? Get(Guid conversationId) =>
            _conversations.TryGetValue(conversationId, out TestConversation? conversation)
                ? conversation.Metadata
                : null;

        public Task<SendMessageResult> SendMessageAsync(
            Guid conversationId,
            string message,
            CancellationToken cancellationToken = default)
        {
            if (!_conversations.TryGetValue(conversationId, out TestConversation? conversation))
            {
                return Task.FromResult(new SendMessageResult(SendMessageOutcome.NotFound));
            }

            if (conversation.Metadata.Status == SessionStatus.Expired)
            {
                return Task.FromResult(new SendMessageResult(SendMessageOutcome.Expired));
            }

            conversation.Messages.Add(message);
            conversation.Metadata = conversation.Metadata with
            {
                LastActivityAt = DateTimeOffset.UtcNow,
                ExpirationTime = DateTimeOffset.UtcNow.AddMinutes(30),
                MessageCount = conversation.Messages.Count,
                Status = SessionStatus.Active
            };
            return Task.FromResult(new SendMessageResult(
                SendMessageOutcome.Success,
                CreateResponse(CreatePlan(string.Join(" ", conversation.Messages)))));
        }

        public IReadOnlyCollection<ConversationMetadata> ListActive() =>
            _conversations.Values.Select(conversation => conversation.Metadata).ToArray();

        public bool Expire(Guid conversationId)
        {
            if (!_conversations.TryGetValue(conversationId, out TestConversation? conversation))
            {
                return false;
            }

            conversation.Metadata = conversation.Metadata with
            {
                ExpirationTime = DateTimeOffset.UtcNow,
                Status = SessionStatus.Expired
            };
            return true;
        }

        public bool Delete(Guid conversationId) => _conversations.TryRemove(conversationId, out _);

        public int CleanupExpired()
        {
            Guid[] expired = _conversations
                .Where(item => item.Value.Metadata.Status == SessionStatus.Expired)
                .Select(item => item.Key)
                .ToArray();
            return expired.Count(Delete);
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

        private static TripPlanResponse CreateResponse(TripPlan plan)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return new(plan, new ExecutionTrace
            {
                StartedAt = now,
                CompletedAt = now,
                TotalDurationMs = 0
            });
        }

        private sealed class TestConversation(Guid id, Guid travelerId, DateTimeOffset createdAt)
        {
            public Guid Id { get; } = id;
            public List<string> Messages { get; } = [];
            public ConversationMetadata Metadata { get; set; } = new(
                id,
                travelerId,
                createdAt,
                createdAt,
                createdAt.AddMinutes(30),
                0,
                SessionStatus.Created);
        }
    }
}
