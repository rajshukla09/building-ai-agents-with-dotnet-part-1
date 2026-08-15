using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartTravelPlanner.Api.Contracts;
using SmartTravelPlanner.Api.Conversations;
using SmartTravelPlanner.Api.Models.Conversations;
using SmartTravelPlanner.Api.Models.TravelPlanning;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class ConversationEndpointTests
{
    [Fact]
    public async Task CreateConversationReturnsCreatedMetadata()
    {
        using TestApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync("/api/conversations", null);
        ConversationMetadata? conversation = await response.Content.ReadFromJsonAsync<ConversationMetadata>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(conversation);
        Assert.NotEqual(Guid.Empty, conversation.ConversationId);
        Assert.Equal(0, conversation.MessageCount);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task MultipleMessagesReuseConversationContext()
    {
        using TestApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        ConversationMetadata conversation = await CreateConversationAsync(client);

        await SendMessageAsync(client, conversation.ConversationId, "Plan a three-day trip to Jaipur.");
        TripPlan revisedPlan = await SendMessageAsync(client, conversation.ConversationId, "Make Day 2 less busy.");
        ConversationMetadata? metadata = await client.GetFromJsonAsync<ConversationMetadata>(
            $"/api/conversations/{conversation.ConversationId}");

        Assert.Contains("Plan a three-day trip to Jaipur.", revisedPlan.Summary);
        Assert.Contains("Make Day 2 less busy.", revisedPlan.Summary);
        Assert.Equal(2, metadata?.MessageCount);
    }

    [Fact]
    public async Task ConversationsRemainIsolated()
    {
        using TestApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        ConversationMetadata first = await CreateConversationAsync(client);
        ConversationMetadata second = await CreateConversationAsync(client);

        await SendMessageAsync(client, first.ConversationId, "Plan Jaipur.");
        TripPlan secondPlan = await SendMessageAsync(client, second.ConversationId, "Plan Kyoto.");

        Assert.Contains("Plan Kyoto.", secondPlan.Summary);
        Assert.DoesNotContain("Jaipur", secondPlan.Summary);
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
    public async Task StreamingReturnsUpdatesProgressivelyAndPreservesContinuity()
    {
        using TestApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        ConversationMetadata conversation = await CreateConversationAsync(client);

        using HttpRequestMessage request = new(
            HttpMethod.Post,
            $"/api/conversations/{conversation.ConversationId}/messages/stream")
        {
            Content = JsonContent.Create(new ConversationMessageRequest("Plan Jaipur."))
        };
        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead);
        using Stream stream = await response.Content.ReadAsStreamAsync();
        using StreamReader reader = new(stream);

        string? generating = await reader.ReadLineAsync();
        string? firstDelta = await reader.ReadLineAsync();
        string? secondDelta = await reader.ReadLineAsync();
        string remainder = await reader.ReadToEndAsync();
        TripPlan nextPlan = await SendMessageAsync(
            client,
            conversation.ConversationId,
            "Make it quieter.");
        ConversationMetadata? metadata = await client.GetFromJsonAsync<ConversationMetadata>(
            $"/api/conversations/{conversation.ConversationId}");

        Assert.Equal("generating", JsonDocument.Parse(generating!).RootElement.GetProperty("status").GetString());
        Assert.Equal("Plan ", JsonDocument.Parse(firstDelta!).RootElement.GetProperty("delta").GetString());
        Assert.Equal("Jaipur.", JsonDocument.Parse(secondDelta!).RootElement.GetProperty("delta").GetString());
        Assert.Contains("completed", remainder);
        Assert.Contains("Plan Jaipur.", nextPlan.Summary);
        Assert.Contains("Make it quieter.", nextPlan.Summary);
        Assert.Equal(2, metadata?.MessageCount);
    }

    [Fact]
    public async Task CancellingStreamDoesNotCommitTurnAndNextMessageStillWorks()
    {
        using TestApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        ConversationMetadata conversation = await CreateConversationAsync(client);
        using CancellationTokenSource cancellation = new();
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            $"/api/conversations/{conversation.ConversationId}/messages/stream")
        {
            Content = JsonContent.Create(new ConversationMessageRequest("Cancel this turn."))
        };
        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellation.Token);
        using Stream stream = await response.Content.ReadAsStreamAsync(cancellation.Token);
        using StreamReader reader = new(stream);

        await reader.ReadLineAsync(cancellation.Token);
        await reader.ReadLineAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await reader.ReadToEndAsync(cancellation.Token));

        // Cancelling a client-side read after response headers does not, by itself,
        // abort TestServer's in-flight response. Close the response stream to model
        // the client disconnect that triggers HttpContext.RequestAborted in production.
        reader.Dispose();
        stream.Dispose();
        response.Dispose();

        await Task.Delay(100);
        TripPlan nextPlan = await SendMessageAsync(client, conversation.ConversationId, "Plan Kyoto.");
        ConversationMetadata? metadata = await client.GetFromJsonAsync<ConversationMetadata>(
            $"/api/conversations/{conversation.ConversationId}");

        Assert.DoesNotContain("Cancel this turn.", nextPlan.Summary);
        Assert.Contains("Plan Kyoto.", nextPlan.Summary);
        Assert.Equal(1, metadata?.MessageCount);
    }

    private static async Task<ConversationMetadata> CreateConversationAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsync("/api/conversations", null);
        return (await response.Content.ReadFromJsonAsync<ConversationMetadata>())!;
    }

    private static async Task<TripPlan> SendMessageAsync(HttpClient client, Guid id, string message)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/conversations/{id}/messages",
            new ConversationMessageRequest(message));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TripPlan>())!;
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
            });
        }
    }

    private sealed class FakeConversationService : IConversationService
    {
        private readonly ConcurrentDictionary<Guid, TestConversation> _conversations = new();

        public Task<ConversationMetadata> CreateAsync(CancellationToken cancellationToken = default)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TestConversation conversation = new(Guid.NewGuid(), now);
            _conversations[conversation.Id] = conversation;
            return Task.FromResult(conversation.Metadata);
        }

        public ConversationMetadata? Get(Guid conversationId) =>
            _conversations.TryGetValue(conversationId, out TestConversation? conversation)
                ? conversation.Metadata
                : null;

        public Task<TripPlan?> SendMessageAsync(
            Guid conversationId,
            string message,
            CancellationToken cancellationToken = default)
        {
            if (!_conversations.TryGetValue(conversationId, out TestConversation? conversation))
            {
                return Task.FromResult<TripPlan?>(null);
            }

            conversation.Messages.Add(message);
            conversation.Metadata = conversation.Metadata with
            {
                LastActivityAt = DateTimeOffset.UtcNow,
                MessageCount = conversation.Messages.Count
            };
            return Task.FromResult<TripPlan?>(CreatePlan(string.Join(" ", conversation.Messages)));
        }

        public IAsyncEnumerable<string>? StreamMessageAsync(
            Guid conversationId,
            string message,
            CancellationToken cancellationToken = default) =>
            _conversations.TryGetValue(conversationId, out TestConversation? conversation)
                ? StreamAsync(conversation, message, cancellationToken)
                : null;

        private static async IAsyncEnumerable<string> StreamAsync(
            TestConversation conversation,
            string message,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            string[] updates = message == "Plan Jaipur."
                ? ["Plan ", "Jaipur."]
                : ["First update", "Second update"];

            foreach (string update in updates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return update;
                await Task.Delay(60, cancellationToken);
            }

            conversation.Messages.Add(message);
            conversation.Metadata = conversation.Metadata with
            {
                LastActivityAt = DateTimeOffset.UtcNow,
                MessageCount = conversation.Messages.Count
            };
        }

        public bool Delete(Guid conversationId) => _conversations.TryRemove(conversationId, out _);

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

        private sealed class TestConversation(Guid id, DateTimeOffset createdAt)
        {
            public Guid Id { get; } = id;
            public List<string> Messages { get; } = [];
            public ConversationMetadata Metadata { get; set; } = new(id, createdAt, createdAt, 0);
        }
    }
}
